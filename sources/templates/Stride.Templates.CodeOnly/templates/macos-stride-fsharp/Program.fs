open Stride.Core.Mathematics
open Stride.Engine
open Stride.Games
open Stride.Graphics
open Stride.Rendering
open Stride.Rendering.Compositing
open Stride.Rendering.Lights
open Stride.Rendering.Materials
open Stride.Rendering.Materials.ComputeColors
open Stride.Rendering.ProceduralModels

let createMaterial (game: Game) (color: Color) =
    let descriptor = MaterialDescriptor()
    descriptor.Attributes.Diffuse <- MaterialDiffuseMapFeature(ComputeColor(color))
    descriptor.Attributes.DiffuseModel <- MaterialDiffuseLambertModelFeature()
    Material.New(game.GraphicsDevice, game.Content, descriptor)

let addCamera (game: Game) (scene: Scene) =
    let cameraComponent = CameraComponent()
    game.SceneSystem.GraphicsCompositor <-
        GraphicsCompositorHelper.CreateDefault(
            false,
            camera = cameraComponent,
            clearColor = System.Nullable<Color4>(Color4(0.52f, 0.70f, 0.95f, 1.0f)),
            graphicsProfile = GraphicsProfile.Level_10_0)

    let camera = new Entity("Camera")
    camera.Add(cameraComponent)
    camera.Transform.Position <- Vector3(0.0f, 3.0f, -6.0f)
    let mutable forward = Vector3.Normalize(Vector3(0.0f, -0.25f, 1.0f))
    let mutable up = Vector3.UnitY
    camera.Transform.Rotation <- Quaternion.LookRotation(&forward, &up)
    scene.Entities.Add(camera)

let addLight (scene: Scene) =
    let light = new LightComponent()
    light.Type <- new LightDirectional()
    light.Intensity <- 1.0f

    let entity = new Entity("Sun")
    entity.Add(light)
    entity.Transform.Rotation <- Quaternion.RotationYawPitchRoll(-0.6f, -0.75f, 0.0f)
    scene.Entities.Add(entity)

let addCube (game: Game) (scene: Scene) =
    let model = CubeProceduralModel(Size = Vector3(1.5f, 1.5f, 1.5f)).Generate(game.Services)
    model.Materials.Add(createMaterial game (Color(245uy, 176uy, 65uy, 255uy)))

    let entity = new Entity("Cube")
    entity.Transform.Position <- Vector3(0.0f, 1.0f, 0.0f)
    entity.Add(ModelComponent(model))
    scene.Entities.Add(entity)

type MyTemplateGame() =
    inherit Game()

    override this.BeginRun() =
        base.BeginRun()

        let scene = this.SceneSystem.SceneInstance.RootScene
        addCamera this scene
        addLight scene
        addCube this scene

[<EntryPoint>]
let main _ =
    use game = new MyTemplateGame()
    game.Run()
    0
