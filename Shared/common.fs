namespace ClientDeploy

[<AutoOpen>]
module Common =

  type MaybeMonad() =
    member __.Bind(x, f) = match x with | Some v -> f v | None -> None
    member __.Return(x) = Some x

  let maybe = MaybeMonad()
