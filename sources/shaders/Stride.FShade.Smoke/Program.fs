open Aardvark.Base
open FShade
open System
open System.Runtime.InteropServices
open System.Threading.Tasks
open Stride.Core.Mathematics
open Stride.Engine
open Stride.FShade
open Stride.Games
open Stride.Graphics
open Stride.Rendering

type ShaderVertex =
    {
        [<StridePosition>]
        Position : V4f

        [<StrideColor>]
        Color : V4f
    }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type RuntimeVertex =
    val mutable Position : Vector4
    val mutable Color : Vector4

    new(position, color) =
        {
            Position = position
            Color = color
        }

[<ReflectedDefinition>]
let vertexPassthrough (v : ShaderVertex) =
    vertex {
        return v
    }

[<ReflectedDefinition>]
let vertexTransform (v : ShaderVertex) =
    vertex {
        let transform : M44f = uniform?PerModel?ModelViewProjTrafo
        return { v with Position = transform * v.Position }
    }

[<ReflectedDefinition>]
let fragmentTransformColor (v : ShaderVertex) =
    fragment {
        let transform : M44f = uniform?PerModel?ModelViewProjTrafo
        return v.Color * V4f(transform.M00, transform.M11, transform.M22, transform.M33)
    }

[<ReflectedDefinition>]
let fragmentColor (v : ShaderVertex) =
    fragment {
        return v.Color
    }

let compileEffect effects =
    let effect =
        effects
        |> Effect.compose

    effect
    |> EffectBytecodeCompiler.compileWithOutputs [
        EffectOutput.color<V4f> 0
    ]

let createFragmentUniformBytecode () =
    compileEffect [
        Effect.ofFunction vertexPassthrough
        Effect.ofFunction fragmentTransformColor
    ]

let createVertexTransformBytecode () =
    compileEffect [
        Effect.ofFunction vertexTransform
        Effect.ofFunction fragmentColor
    ]

type FShadeSmokeGame(caseName, bytecode) as this =
    inherit Game()

    let mutable effectInstance : EffectInstance = null
    let mutable pipelineState : MutablePipelineState = null
    let mutable vertexBuffer : Buffer = null
    let mutable renderTarget : Texture = null
    let mutable frame = 0
    let mutable readbackSucceeded = false

    do
        this.GraphicsDeviceManager.PreferredBackBufferWidth <- 320
        this.GraphicsDeviceManager.PreferredBackBufferHeight <- 240
        this.GraphicsDeviceManager.PreferredGraphicsProfile <- [| GraphicsProfile.Level_11_0 |]

    member _.ReadbackSucceeded = readbackSucceeded

    override this.LoadContent() =
        let baseLoadContent = base.LoadContent()
        task {
            do! baseLoadContent

            let vertices =
                [|
                    RuntimeVertex(Vector4(-0.8f, -0.8f, 0.0f, 1.0f), Vector4(1.0f, 0.0f, 0.0f, 1.0f))
                    RuntimeVertex(Vector4( 0.8f, -0.8f, 0.0f, 1.0f), Vector4(0.0f, 1.0f, 0.0f, 1.0f))
                    RuntimeVertex(Vector4( 0.0f,  0.8f, 0.0f, 1.0f), Vector4(0.0f, 0.0f, 1.0f, 1.0f))
                |]

            vertexBuffer <- Buffer.Vertex.New(this.GraphicsDevice, vertices, GraphicsResourceUsage.Default)
            renderTarget <-
                Texture.New2D(
                    this.GraphicsDevice,
                    64,
                    64,
                    PixelFormat.R8G8B8A8_UNorm,
                    TextureFlags.ShaderResource ||| TextureFlags.RenderTarget)

            effectInstance <- new EffectInstance(new Effect(this.GraphicsDevice, bytecode))
            effectInstance.UpdateEffect(this.GraphicsDevice) |> ignore
            effectInstance.SetFShade("ModelViewProjTrafo", Matrix.Identity)

            pipelineState <- new MutablePipelineState(this.GraphicsDevice)
        } :> Task

    override this.Draw(gameTime : GameTime) =
        base.Draw(gameTime)

        frame <- frame + 1

        let commandList = this.GraphicsContext.CommandList
        commandList.Clear(renderTarget, Color4.Black)
        commandList.SetRenderTargetAndViewport(null, renderTarget)

        pipelineState.State.SetDefaults()
        pipelineState.State.RootSignature <- effectInstance.RootSignature
        pipelineState.State.EffectBytecode <- effectInstance.Effect.Bytecode
        pipelineState.State.InputElements <-
            VertexDeclaration(
                VertexElement.Position<Vector4>(),
                VertexElement.Color<Vector4>()).CreateInputElements()
        pipelineState.State.PrimitiveType <- PrimitiveType.TriangleList
        pipelineState.State.RasterizerState <- RasterizerStates.CullNone
        pipelineState.State.Output.CaptureState(commandList)
        pipelineState.Update()

        commandList.SetPipelineState(pipelineState.CurrentState)
        commandList.SetVertexBuffer(0, vertexBuffer, 0, Marshal.SizeOf<RuntimeVertex>())
        effectInstance.Apply(this.GraphicsContext)
        commandList.Draw(3)

        if frame >= 2 then
            let pixels = renderTarget.GetData<byte>(commandList)
            readbackSucceeded <-
                pixels
                |> Array.chunkBySize 4
                |> Array.exists (fun rgba -> rgba.Length = 4 && (int rgba[0] + int rgba[1] + int rgba[2]) > 32)

            printfn "FShade %s render readback nonblack=%b bytes=%d" caseName readbackSucceeded pixels.Length
            this.Exit()

    override this.Destroy() =
        if not (isNull vertexBuffer) then vertexBuffer.Dispose()
        if not (isNull renderTarget) then renderTarget.Dispose()
        if not (isNull effectInstance) then effectInstance.Dispose()
        base.Destroy()

[<EntryPoint>]
let main _ =
    Aardvark.Init()

    let cases =
        [
            "fragment-uniform", createFragmentUniformBytecode
            "vertex-transform", createVertexTransformBytecode
        ]

    let mutable allSucceeded = true

    for caseName, createBytecode in cases do
        let bytecode = createBytecode ()

        printfn "Stride FShade %s EffectBytecode stages=%d id=%O" caseName bytecode.Stages.Length (bytecode.ComputeId())
        for input in bytecode.Reflection.InputAttributes do
            printfn "  input location=%d semantic=%s%d" input.Location input.SemanticName input.SemanticIndex

        if bytecode.Reflection.ConstantBuffers.Count = 0 then
            failwithf "Expected the FShade %s shader to reflect a uniform constant buffer." caseName

        for constantBuffer in bytecode.Reflection.ConstantBuffers do
            printfn "  cbuffer %s size=%d members=%d" constantBuffer.Name constantBuffer.Size constantBuffer.Members.Length
            for memberInfo in constantBuffer.Members do
                printfn "    member %s key=%s offset=%d size=%d" memberInfo.RawName memberInfo.KeyInfo.KeyName memberInfo.Offset memberInfo.Size

        use game = new FShadeSmokeGame(caseName, bytecode)
        game.Run()

        allSucceeded <- allSucceeded && game.ReadbackSucceeded

    if allSucceeded then 0 else 1
