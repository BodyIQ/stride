namespace Stride.FShade

open System
open Stride.Rendering
open Stride.Shaders

[<RequireQualifiedAccess>]
module FShadeParameters =
    let private candidateNames (constantBuffer : EffectConstantBufferDescription) (memberInfo : EffectValueDescription) =
        seq {
            memberInfo.KeyInfo.KeyName
            memberInfo.RawName
            if not (String.IsNullOrWhiteSpace constantBuffer.Name) && not (String.IsNullOrWhiteSpace memberInfo.RawName) then
                $"{constantBuffer.Name}.{memberInfo.RawName}"
        }
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)

    let private matchingMembers name (reflection : EffectReflection) =
        reflection.ConstantBuffers
        |> Seq.collect (fun constantBuffer ->
            constantBuffer.Members
            |> Seq.filter (fun memberInfo ->
                candidateNames constantBuffer memberInfo
                |> Seq.exists (fun candidate -> String.Equals(candidate, name, StringComparison.Ordinal)))
            |> Seq.map (fun memberInfo -> constantBuffer, memberInfo))
        |> Seq.toList

    let tryFindValueKey<'T when 'T : unmanaged and 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (reflection : EffectReflection) (name : string) : ValueParameterKey<'T> option =
        match matchingMembers name reflection with
        | [] ->
            None
        | [_, memberInfo] ->
            match memberInfo.KeyInfo.Key with
            | :? ValueParameterKey<'T> as key -> Some key
            | _ -> None
        | matches ->
            let locations =
                matches
                |> List.map (fun (constantBuffer, memberInfo) -> $"{constantBuffer.Name}.{memberInfo.RawName}")
                |> String.concat ", "

            invalidOp $"FShade uniform name '{name}' is ambiguous. Matches: {locations}"

    let findValueKey<'T when 'T : unmanaged and 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)> (reflection : EffectReflection) (name : string) : ValueParameterKey<'T> =
        match tryFindValueKey<'T> reflection name with
        | Some key -> key
        | None -> invalidOp $"Could not resolve FShade uniform '{name}' as {typeof<'T>.Name}."

[<AutoOpen>]
module EffectInstanceExtensions =
    type EffectInstance with
        member effectInstance.TryGetFShade<'T when 'T : unmanaged and 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)>(name : string) =
            FShadeParameters.tryFindValueKey<'T> effectInstance.Effect.Bytecode.Reflection name

        member effectInstance.SetFShade<'T when 'T : unmanaged and 'T : struct and 'T :> ValueType and 'T : (new : unit -> 'T)>(name : string, value : 'T) =
            let key : ValueParameterKey<'T> = FShadeParameters.findValueKey<'T> effectInstance.Effect.Bytecode.Reflection name
            effectInstance.Parameters.Set<'T>(key, value)
