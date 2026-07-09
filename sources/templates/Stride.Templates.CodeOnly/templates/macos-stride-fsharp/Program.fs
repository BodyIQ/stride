open Stride.CommunityToolkit.Engine
open Stride.CommunityToolkit.Games
open Stride.CommunityToolkit.Rendering.ProceduralModels
open Stride.CommunityToolkit.Skyboxes
open Stride.Core.Mathematics
open Stride.Engine

let game = new Game()

let start (rootScene: Scene) =
    game.SetupBase3D()
    game.Add3DCameraController() |> ignore
    game.AddSkybox() |> ignore

    let capsule = game.Create3DPrimitive(PrimitiveModelType.Capsule)
    capsule.Transform.Position <- Vector3(0f, 2.5f, 0f)
    capsule.Scene <- rootScene

[<EntryPoint>]
let main _ =
    game.Run(start = start)
    0
