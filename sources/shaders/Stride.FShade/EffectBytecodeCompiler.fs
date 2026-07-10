namespace Stride.FShade

open System
open System.Text
open Microsoft.FSharp.NativeInterop
open FShade
open FShade.GLSL
open Silk.NET.Shaderc
open Stride.Core.Storage
open Stride.Shaders

#nowarn "9"

type CompileOptions =
    {
        SourceName : string
        VulkanVersion : EnvVersion
        SpirvVersion : SpirvVersion
        OptimizationLevel : OptimizationLevel
    }

[<RequireQualifiedAccess>]
module CompileOptions =
    let Default =
        {
            SourceName = "fshade.glsl"
            VulkanVersion = EnvVersion.Vulkan13
            SpirvVersion = SpirvVersion.Shaderc13
            OptimizationLevel = OptimizationLevel.Performance
        }

[<RequireQualifiedAccess>]
module EffectBytecodeCompiler =
    let private entryPointBytes (name : string) =
        let bytes = Array.zeroCreate<byte> (Encoding.UTF8.GetByteCount(name) + 1)
        Encoding.UTF8.GetBytes(name.AsSpan(), bytes.AsSpan()) |> ignore
        bytes

    let private normalizeSemantic (semantic : string) =
        match semantic.ToUpperInvariant() with
        | "POSITIONS" -> "POSITION"
        | "COLORS" -> "COLOR"
        | "NORMALS" -> "NORMAL"
        | "TANGENTS" -> "TANGENT"
        | "BINORMALS" -> "BINORMAL"
        | "TEXCOORDS"
        | "TEXCOORDINATES" -> "TEXCOORD"
        | normalized -> normalized

    let private semanticParts (semantic : string) =
        let semantic = normalizeSemantic semantic
        let mutable split = semantic.Length
        while split > 0 && Char.IsDigit(semantic[split - 1]) do
            split <- split - 1

        if split = semantic.Length then
            semantic, 0
        else
            semantic.Substring(0, split), Int32.Parse(semantic.Substring(split))

    let private shaderStages (stage : FShade.ShaderStage) =
        match stage with
        | FShade.ShaderStage.Vertex ->
            "Vertex", ShaderKind.VertexShader, Stride.Shaders.ShaderStage.Vertex
        | FShade.ShaderStage.Fragment ->
            "Fragment", ShaderKind.FragmentShader, Stride.Shaders.ShaderStage.Pixel
        | FShade.ShaderStage.Geometry ->
            "Geometry", ShaderKind.GeometryShader, Stride.Shaders.ShaderStage.Geometry
        | FShade.ShaderStage.TessControl ->
            "TessControl", ShaderKind.TessControlShader, Stride.Shaders.ShaderStage.Hull
        | FShade.ShaderStage.TessEval ->
            "TessEval", ShaderKind.TessEvaluationShader, Stride.Shaders.ShaderStage.Domain
        | FShade.ShaderStage.Compute ->
            "Compute", ShaderKind.ComputeShader, Stride.Shaders.ShaderStage.Compute
        | other ->
            failwithf "Unsupported FShade stage: %A" other

    let private shaderStageFlag (stage : FShade.ShaderStage) =
        let _, _, strideStage = shaderStages stage
        ShaderStageExtensions.ToFlag(strideStage)

    let private effectParameterType (typ : GLSLType) =
        match typ with
        | GLSLType.Bool -> EffectParameterType.Bool, 4
        | GLSLType.Int(true, _) -> EffectParameterType.Int, 4
        | GLSLType.Int(false, _) -> EffectParameterType.UInt, 4
        | GLSLType.Float 32 -> EffectParameterType.Float, 4
        | GLSLType.Float 64 -> EffectParameterType.Double, 8
        | other -> failwithf "Unsupported FShade uniform scalar type: %A" other

    let rec private effectTypeDescription (typ : GLSLType) =
        match typ with
        | GLSLType.Bool
        | GLSLType.Int _
        | GLSLType.Float _ ->
            let parameterType, elementSize = effectParameterType typ
            EffectTypeDescription(
                Class = EffectParameterClass.Scalar,
                Type = parameterType,
                RowCount = 1,
                ColumnCount = 1,
                ElementSize = elementSize)
        | GLSLType.Vec(dimension, elementType) ->
            let scalarType = effectTypeDescription elementType
            EffectTypeDescription(
                Class = EffectParameterClass.Vector,
                Type = scalarType.Type,
                RowCount = 1,
                ColumnCount = dimension,
                ElementSize = scalarType.ElementSize)
        | GLSLType.Mat(columns, rows, elementType) ->
            let scalarType = effectTypeDescription elementType
            EffectTypeDescription(
                Class = EffectParameterClass.MatrixColumns,
                Type = scalarType.Type,
                RowCount = columns,
                ColumnCount = rows,
                ElementSize = scalarType.ElementSize)
        | other ->
            failwithf "Unsupported FShade uniform type: %A" other

    let rec private uniformValueSize (typ : GLSLType) =
        match typ with
        | GLSLType.Bool -> 4
        | GLSLType.Int _ -> 4
        | GLSLType.Float 32 -> 4
        | GLSLType.Float 64 -> 8
        | GLSLType.Vec(dimension, elementType) -> dimension * uniformValueSize elementType
        | GLSLType.Mat(columns, rows, elementType) -> columns * rows * uniformValueSize elementType
        | GLSLType.Array(length, _, stride) -> length * stride
        | GLSLType.Struct(_, _, size) -> size
        | other -> failwithf "Unsupported FShade uniform value size for type: %A" other

    let private keyName uniformBufferName fieldName =
        match uniformBufferName, fieldName with
        | _, "ModelViewProjTrafo" -> "Transformation.WorldViewProjection"
        | _ -> $"{uniformBufferName}.{fieldName}"

    let private addUniformBuffers (programInterface : GLSLProgramInterface) (reflection : EffectReflection) =
        for KeyValue(_, uniformBuffer) in GLSLProgramInterface.uniformBuffers programInterface do
            let stages =
                programInterface.GetUniformStages(uniformBuffer.ubName)
                |> Seq.fold (fun flags stage -> flags ||| shaderStageFlag stage) ShaderStageFlags.None

            let members =
                uniformBuffer.ubFields
                |> List.map (fun field ->
                    let valueType = effectTypeDescription field.ufType
                    EffectValueDescription(
                        Type = valueType,
                        RawName = field.ufName,
                        KeyInfo = EffectParameterKeyInfo(KeyName = keyName uniformBuffer.ubName field.ufName),
                        Offset = field.ufOffset,
                        Size = uniformValueSize field.ufType,
                        LogicalGroup = uniformBuffer.ubName))
                |> List.toArray

            let constantBuffer =
                EffectConstantBufferDescription(
                    Name = uniformBuffer.ubName,
                    Size = uniformBuffer.ubSize,
                    Type = ConstantBufferType.ConstantBuffer,
                    Members = members)

            let binding =
                EffectResourceBindingDescription(
                    KeyInfo = EffectParameterKeyInfo(KeyName = uniformBuffer.ubName),
                    Class = EffectParameterClass.ConstantBuffer,
                    Type = EffectParameterType.ConstantBuffer,
                    RawName = uniformBuffer.ubName,
                    ResourceGroup = uniformBuffer.ubName,
                    Stage = ShaderStage.None,
                    SlotStart = uniformBuffer.ubBinding,
                    SlotCount = 1,
                    LogicalGroup = uniformBuffer.ubName)

            let entry = EffectResourceEntry(&binding)
            let mutable entry = entry
            entry.Stages <- stages

            let group = EffectResourceGroupDescription(Name = uniformBuffer.ubName, ConstantBuffer = constantBuffer)
            group.Entries.Add(entry)

            reflection.ConstantBuffers.Add(constantBuffer)
            reflection.ResourceBindings.Add(binding)
            reflection.ResourceGroups.Add(group)

    let private compileSpirv (options : CompileOptions) (glsl : string) (stage : FShade.ShaderStage) =
        let define, shaderKind, strideStage = shaderStages stage
        let api = Shaderc.GetApi()
        let compiler = api.CompilerInitialize()
        if NativePtr.toNativeInt compiler = nativeint 0 then
            failwith "shaderc compiler initialization failed"

        let shadercOptions = api.CompileOptionsInitialize()
        if NativePtr.toNativeInt shadercOptions = nativeint 0 then
            api.CompilerRelease(compiler)
            failwith "shaderc compile options initialization failed"

        let mutable compiledStage = None

        try
            api.CompileOptionsSetSourceLanguage(shadercOptions, SourceLanguage.Glsl)
            api.CompileOptionsSetTargetEnv(shadercOptions, TargetEnv.Vulkan, uint32 options.VulkanVersion)
            api.CompileOptionsSetTargetSpirv(shadercOptions, options.SpirvVersion)
            api.CompileOptionsSetOptimizationLevel(shadercOptions, options.OptimizationLevel)
            api.CompileOptionsAddMacroDefinition(shadercOptions, define, unativeint define.Length, "", unativeint 0)

            let result =
                api.CompileIntoSpv(
                    compiler,
                    glsl,
                    unativeint (Encoding.UTF8.GetByteCount(glsl)),
                    shaderKind,
                    options.SourceName,
                    "main",
                    shadercOptions)

            if NativePtr.toNativeInt result = nativeint 0 then
                failwithf "shaderc returned a null result for %s" define

            try
                let status = api.ResultGetCompilationStatus(result)
                if status <> CompilationStatus.Success then
                    failwithf "shaderc failed for %s: %A\n%s" define status (api.ResultGetErrorMessageS(result))

                let length = int (api.ResultGetLength(result))
                let source = api.ResultGetBytes(result)
                let bytes = Array.zeroCreate<byte> length
                for i in 0 .. length - 1 do
                    bytes[i] <- NativePtr.get source i

                compiledStage <- Some (strideStage, bytes)
            finally
                api.ResultRelease(result)
        finally
            api.CompileOptionsRelease(shadercOptions)
            api.CompilerRelease(compiler)

        match compiledStage with
        | Some compiled -> compiled
        | None -> failwithf "shaderc did not produce bytecode for %s" define

    let compileWithOptions (options : CompileOptions) (config : EffectConfig) (effect : Effect) =
        let module_ = effect |> Effect.toModule config
        let layout = EffectInputLayout.ofModule module_
        let moduleWithLayout = module_ |> EffectInputLayout.apply layout
        let glsl = moduleWithLayout |> ModuleCompiler.compileGLSLVulkan

        if String.Equals(Environment.GetEnvironmentVariable("STRIDE_FSHADE_DUMP_GLSL"), "1", StringComparison.Ordinal) then
            Console.Error.WriteLine(glsl.iface.ToString())
            Console.Error.WriteLine(glsl.code)

        match GLSLProgramInterface.shaders glsl.iface with
        | GLSLProgramShaders.Graphics graphics ->
            let stages =
                graphics.stages
                |> Seq.map (fun kvp ->
                    let strideStage, bytes = compileSpirv options glsl.code kvp.Key
                    ShaderBytecode(
                        strideStage,
                        ObjectId.FromBytes(bytes : byte[]),
                        bytes,
                        EntryPoint = entryPointBytes kvp.Value.shaderEntry))
                |> Seq.toArray

            let reflection = EffectReflection()
            for input in GLSLProgramInterface.inputs glsl.iface do
                let semanticName, semanticIndex = semanticParts input.paramSemantic
                reflection.InputAttributes.Add(
                    ShaderInputAttributeDescription(
                        Location = input.paramLocation,
                        SemanticName = semanticName,
                        SemanticIndex = semanticIndex))

            addUniformBuffers glsl.iface reflection

            EffectBytecode(
                Reflection = reflection,
                HashSources = HashSourceCollection(),
                Stages = stages)
        | other ->
            failwithf "Unsupported FShade shader set: %A" other

    let compileWithConfig (config : EffectConfig) (effect : Effect) =
        compileWithOptions CompileOptions.Default config effect

    let compileWithOutputs (outputs : seq<EffectOutput>) (effect : Effect) =
        compileWithConfig (EffectConfiguration.create outputs) effect

    let compile (effect : Effect) =
        compileWithConfig (EffectConfig.ofList []) effect
