namespace Ionide.VSCode.FSharp

//---------------------------------------------------
//Find path of F# install and FSI path
//---------------------------------------------------
// Below code adapted from similar in FSAutoComplete project
module Environment =

    open Fable.Core
    open Fable.Core.JsInterop
    open Ionide.VSCode.Helpers

    module node = Node.Api

    let isWin = node.``process``.platform = Node.Base.Platform.Win32

    let private (</>) a b =
        if isWin then a + @"\" + b else a + "/" + b

    let configFsiFilePath () =
        Configuration.tryGet "FSharp.fsiFilePath"

    let configFsiSdkFilePath () =
        Configuration.tryGet "FSharp.fsiSdkFilePath"

    // because the buffers from console output contain newlines, we need to trim them out if we want to have usable path inputs
    let spawnAndGetTrimmedOutput location command =
        Process.exec location command
        |> Promise.map (fun (err, stdoutBuf, stderrBuf) ->
            err, stdoutBuf |> string |> String.trim, stderrBuf |> string |> String.trim)

    let tryGetTool toolName =
        if isWin then
            spawnAndGetTrimmedOutput "cmd" (ResizeArray [ "/C"; "where"; toolName ])
            |> Promise.map (fun (err, path, errs) -> if path <> "" then Some path else None)
            |> Promise.map (Option.bind (fun paths -> paths.Split('\n') |> Array.map (String.trim) |> Array.tryHead))
        else
            spawnAndGetTrimmedOutput "which" (ResizeArray [ toolName ])
            |> Promise.map (fun (err, path, errs) -> if path <> "" then Some path else None)

    let dotnet =
        Configuration.tryGet "FSharp.dotnetRoot"
        |> Option.map (fun root -> root </> (if isWin then "dotnet.exe" else "dotnet") |> Some |> Promise.lift)
        |> Option.defaultWith (fun () -> tryGetTool "dotnet")


    let ensureDirectory (path: string) =
        let root =
            if node.path.isAbsolute path then
                None
            else
                Some node.__dirname

        let segments = path.Split [| char node.path.sep |] |> Array.toList

        let rec ensure segments currentPath =
            match segments with
            | head :: tail ->
                if head = "" then
                    ensure tail currentPath
                else
                    let subPath =
                        match currentPath with
                        | Some path -> node.path.join (path, head)
                        | None -> head

                    if not (node.fs.existsSync !^subPath) then
                        node.fs.mkdirSync subPath

                    ensure tail (Some subPath)
            | [] -> ()

        ensure segments root
        path


    /// expand any env variables in a string according to
    /// .NET's rules - that is any %-encoded name is an env var
    [<Emit("$0.replace(/%([^%]+)%/g, (_,n) => process.env[n])")>]
    let expand (s: string) : string = jsNative
