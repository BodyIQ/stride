namespace Stride.FShade

open System
open Aardvark.Base
open FShade

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StridePositionAttribute() =
    inherit PositionAttribute()

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StrideColorAttribute() =
    inherit SemanticAttribute("COLOR")

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StrideNormalAttribute() =
    inherit SemanticAttribute("NORMAL")

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StrideTangentAttribute() =
    inherit SemanticAttribute("TANGENT")

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StrideBinormalAttribute() =
    inherit SemanticAttribute("BINORMAL")

[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field, AllowMultiple = false)>]
type StrideTexCoordAttribute(index : int) =
    inherit SemanticAttribute(sprintf "TEXCOORD%d" index)

    new() = StrideTexCoordAttribute(0)

type EffectOutput =
    {
        Semantic : string
        Type : Type
        Location : int
    }

[<RequireQualifiedAccess>]
module EffectOutput =
    let create semantic ``type`` location =
        {
            Semantic = semantic
            Type = ``type``
            Location = location
        }

    let color<'T> location =
        create "Colors" typeof<'T> location

    let depth<'T> location =
        create "Depth" typeof<'T> location

[<RequireQualifiedAccess>]
module EffectConfiguration =
    let create (outputs : seq<EffectOutput>) =
        outputs
        |> Seq.map (fun output -> output.Semantic, output.Type, output.Location)
        |> EffectConfig.ofSeq

    let color<'T> =
        EffectOutput.color<'T> 0
        |> Seq.singleton
        |> create

[<AutoOpen>]
module UniformAliases =
    type UniformScope with
        member inline x.ModelTrafo : M44f = x?PerModel?ModelTrafo
        member inline x.ViewTrafo : M44f = x?PerView?ViewTrafo
        member inline x.ProjTrafo : M44f = x?PerView?ProjTrafo
        member inline x.ViewProjTrafo : M44f = x?PerView?ViewProjTrafo
        member inline x.ModelViewTrafo : M44f = x?PerModel?ModelViewTrafo
        member inline x.ModelViewProjTrafo : M44f = x?PerModel?ModelViewProjTrafo
        member inline x.NormalMatrix : M33f = x?PerModel?NormalMatrix

        member inline x.ModelTrafoInv : M44f = x?PerModel?ModelTrafoInv
        member inline x.ViewTrafoInv : M44f = x?PerView?ViewTrafoInv
        member inline x.ProjTrafoInv : M44f = x?PerView?ProjTrafoInv
        member inline x.ViewProjTrafoInv : M44f = x?PerView?ViewProjTrafoInv
        member inline x.ModelViewTrafoInv : M44f = x?PerModel?ModelViewTrafoInv
        member inline x.ModelViewProjTrafoInv : M44f = x?PerModel?ModelViewProjTrafoInv

        member inline x.CameraLocation : V3f = x?PerView?CameraLocation
        member inline x.LightLocation : V3f = x?PerLight?LightLocation
        member inline x.ViewportSize : V2i = x?PerView?ViewportSize

        member inline x.DiffuseColor : V4f = x?PerMaterial?DiffuseColor
        member inline x.AmbientColor : V4f = x?PerMaterial?AmbientColor
        member inline x.EmissiveColor : V4f = x?PerMaterial?EmissiveColor
        member inline x.SpecularColor : V4f = x?PerMaterial?SpecularColor
        member inline x.Shininess : float32 = x?PerMaterial?Shininess
