open Aardvark.Base
open FShade
open System
open System.Runtime.InteropServices
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
let vertexMain (v : ShaderVertex) =
    vertex {
        let worldViewProjection : M44f = uniform?PerModel?ModelViewProjTrafo
        return { v with Position = worldViewProjection * v.Position }
    }

[<ReflectedDefinition>]
let fragmentMain (v : ShaderVertex) =
    fragment {
        return v.Color
    }

let createEffectBytecode () =
    [
        Effect.ofFunction vertexMain
        Effect.ofFunction fragmentMain
    ]
    |> Effect.compose
    |> EffectBytecodeCompiler.compileWithOutputs [
        EffectOutput.color<V4f> 0
    ]

type FShadeCodeOnlyGame() as this =
    inherit Game()

    let effectBytecode = createEffectBytecode ()
    let mutable effectInstance : EffectInstance = null
    let mutable pipelineState : MutablePipelineState = null
    let mutable vertexBuffer : Buffer = null

    do
        this.GraphicsDeviceManager.PreferredBackBufferWidth <- 1280
        this.GraphicsDeviceManager.PreferredBackBufferHeight <- 720
        this.GraphicsDeviceManager.PreferredGraphicsProfile <- [| GraphicsProfile.Level_11_0 |]

    override this.LoadContent() =
        let baseLoadContent = base.LoadContent()

        task {
            do! baseLoadContent

            let vertices =
                [|
                    RuntimeVertex(Vector4( 0.0f,  0.65f, 0.0f, 1.0f), Vector4(0.95f, 0.25f, 0.25f, 1.0f))
                    RuntimeVertex(Vector4(-0.7f, -0.55f, 0.0f, 1.0f), Vector4(0.20f, 0.85f, 0.45f, 1.0f))
                    RuntimeVertex(Vector4( 0.7f, -0.55f, 0.0f, 1.0f), Vector4(0.25f, 0.45f, 1.00f, 1.0f))
                |]

            vertexBuffer <- Buffer.Vertex.New(this.GraphicsDevice, vertices, GraphicsResourceUsage.Default)
            effectInstance <- new EffectInstance(new Effect(this.GraphicsDevice, effectBytecode))
            effectInstance.UpdateEffect(this.GraphicsDevice) |> ignore

            pipelineState <- new MutablePipelineState(this.GraphicsDevice)
        }
        :> Threading.Tasks.Task

    override this.Draw(gameTime : GameTime) =
        base.Draw(gameTime)

        let commandList = this.GraphicsContext.CommandList
        commandList.Clear(commandList.RenderTarget, Color4(0.03f, 0.04f, 0.055f, 1.0f))

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

        let rotation = Matrix.RotationZ(float32 gameTime.Total.TotalSeconds)
        effectInstance.SetFShade("ModelViewProjTrafo", rotation)

        commandList.SetPipelineState(pipelineState.CurrentState)
        commandList.SetVertexBuffer(0, vertexBuffer, 0, Marshal.SizeOf<RuntimeVertex>())
        effectInstance.Apply(this.GraphicsContext)
        commandList.Draw(3)

    override this.Destroy() =
        if not (isNull vertexBuffer) then vertexBuffer.Dispose()
        if not (isNull effectInstance) then effectInstance.Dispose()
        base.Destroy()

[<EntryPoint>]
let main _ =
    Aardvark.Init()

    use game = new FShadeCodeOnlyGame()
    game.Run()
    0
