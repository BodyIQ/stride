namespace MyTemplate

open Stride.CommunityToolkit.Engine
open Stride.CommunityToolkit.Games
open Stride.CommunityToolkit.Rendering.ProceduralModels
open Stride.CommunityToolkit.Skyboxes
open Stride.Core.Mathematics
open Stride.Engine
open Stride.Games

module private SceneSetup =
    let configure (game: Game) (rootScene: Scene) =
        game.SetupBase3D()
        game.Add3DCameraController() |> ignore
        game.AddSkybox() |> ignore

        let capsule = game.Create3DPrimitive(PrimitiveModelType.Capsule)
        capsule.Transform.Position <- Vector3(0f, 2.5f, 0f)
        capsule.Scene <- rootScene

type GameApplication =
    static member CreateGame() =
        new Game()

    static member Run(game: Game, context: GameContext) =
        game.Run(context = context, start = fun rootScene -> SceneSetup.configure game rootScene)
