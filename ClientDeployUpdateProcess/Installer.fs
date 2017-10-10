namespace ClientDeploy

module Installer =
  open Semver
  open System.IO
  open Manifests
  open System.Net
  open System.Diagnostics

  type private InstallerAction =
    | KillProcess of int
    | CreateFolder of string
    | DownloadResource of string*int*string*string
    | Backup of string*string
    | CreateFileFromResource of string*(string)*int*string
    | DeleteFile of string
    | DeleteTempFile of string
    | DeleteFolderContent of string
    | DeleteFolder of string
    | WriteConfig of string*((string*string) list)
    | Error of string
    | StartProcess of (string*string)


  let hasher = System.Security.Cryptography.MD5.Create()

  let hashFile f =
    let data = System.IO.File.ReadAllBytes f
    let hash = hasher.ComputeHash data
    System.Guid(hash).ToString()


  let private check_expectations target expectations (kill:int option) (start:(string*string) option) uuid repo product version : (int*InstallerAction) list = 

    let replace (p:string) : string = p.Replace(Manifests.INSTALLTARGET,target)

    let mkFile (p:string) (f:string) : string = System.IO.Path.Combine((replace p),(replace f))

    let folders_to_create =
      expectations
      |> List.choose
          ( function            
            | FolderExpected (p) -> Some (replace p)
            | FileExpected (p,_) -> Some (System.IO.Path.GetDirectoryName((replace p)))
            | ExactFileExpected (p,_,_) -> Some (System.IO.Path.GetDirectoryName((replace p)))
            | _ -> None )
      |> List.filter (fun p -> System.IO.Directory.Exists p |> not)
      |> List.map (fun p -> 0,(InstallerAction.CreateFolder p))
      |> List.distinct

    let files_to_copy =
      expectations
      |> List.collect
          ( function
            | FileExpected (file,{Source=(source,_);Size=size;Hash=hash}) ->
              let file = replace file
              if System.IO.File.Exists file 
                then []
                else 
                  [ 1,(InstallerAction.DownloadResource (hash,size,source,file))
                    4,(InstallerAction.CreateFileFromResource (file,(source),size,hash))
                    8,(InstallerAction.DeleteTempFile hash) ]
            | ExactFileExpected (file,hash,{Source=(source,_);Size=size;Hash=_}) ->
              let file = replace file
              if System.IO.File.Exists file
              then
                let fhash = hashFile file
                if hash=fhash 
                then []
                else 
                  [ 1,(InstallerAction.DownloadResource (hash,size,source,file))
                    3,(InstallerAction.Backup (file,hash+".backup"))
                    4,(InstallerAction.CreateFileFromResource (file,(source),size,hash))
                    8,(InstallerAction.DeleteTempFile hash)
                    8,(InstallerAction.DeleteTempFile (hash+".backup")) ]
              else 
                  [ 1,(InstallerAction.DownloadResource (hash,size,source,file))
                    4,(InstallerAction.CreateFileFromResource (file,(source),size,hash))
                    8,(InstallerAction.DeleteTempFile hash) ]
                
            | _ -> [])

    let other =
        [ kill |> Option.map (fun pid -> 1,(InstallerAction.KillProcess pid))
          start |> Option.map (fun cmd -> 9,(InstallerAction.StartProcess cmd))
          Some (8,(InstallerAction.WriteConfig(".clientdeploy.config",[ ("uuid",uuid) ; ("repo",repo) ; ("product",product) ; ("version",version) ])))] 

    let candidates = [ folders_to_create ; files_to_copy ] |> List.concat
    let errors = candidates |> List.filter (function | (_,Error _) -> true | _ -> false)
    if (errors |> List.isEmpty |> not) 
    then errors 
    elif (candidates |> List.isEmpty)
    then []
    else ((List.choose id other) @ candidates)


  let private renderStep =
    function
    | Error info -> printfn "Unable to proceed with installation: '%s'" info
    | CreateFolder path -> printfn "Create folder '%s'" path
    | DownloadResource (hash,size,source,file) -> printfn "Download file '%s' (%d bytes) from %s" file size  source
    | CreateFileFromResource (file,(_),_,_) -> printfn "Copy file '%s'" file 
    | Backup (f,t) -> printfn "Backup file %s" f
    | DeleteFile (file) -> printfn "Delete file '%s'" file
    | DeleteTempFile (file) -> printfn "Delete temporary file '%s'" file
    | DeleteFolderContent (path) -> printfn "Delete all files in folder '%s'" path
    | DeleteFolder (path) -> printfn "Remove folder '%s'" path
    | WriteConfig (file,kvlist) -> printfn "Write config to '%s'" file
    | KillProcess pid -> printfn "Kill procedd with pid %d" pid
    | StartProcess (exe,_) -> printfn "Start process '%s'" exe

  let private render =
    function
    | [] -> printfn "Nothing to do..."
    | steps -> [0..9] |> List.iter(fun level -> steps |> List.filter (fun (l,_)->l=level) |> List.iter (snd>>renderStep))


  let private executeStep (wc:WebClient) (step:InstallerAction) =
    renderStep step
    match step with
    | Error _ -> ()
    | KillProcess pid -> 
       let p = System.Diagnostics.Process.GetProcessById(pid)
       if p<>null
        then
          p.Kill()
          if (not (p.WaitForExit(5000))) then failwithf "Unable to terminate process with pid %d" pid
        else ()
    | StartProcess (exe,args) ->
      let psi = new ProcessStartInfo(exe,args)
      Process.Start(psi) |> ignore
    | CreateFolder p -> System.IO.Directory.CreateDirectory(p) |> ignore
    | DownloadResource (hash,size,source,file) -> wc.DownloadFile(source,hash)
    | CreateFileFromResource (file,(source),size,hash) -> File.Copy(hash,file,true)
    | Backup (f,t) -> System.IO.File.Copy(f,t)
    | DeleteFile f -> System.IO.File.Delete(f)
    | DeleteTempFile f -> System.IO.File.Delete(f)
    | DeleteFolderContent p -> System.IO.Directory.Delete(p,true)
    | DeleteFolder p -> System.IO.Directory.Delete(p)
    | WriteConfig (file,kvl) ->
      System.IO.File.WriteAllLines(file,(kvl |> List.map (fun (k,v)->sprintf "%s|%s" k v)))


  let private execute steps =
    use wc = new System.Net.WebClient()
    [0..9] |> List.iter(fun level -> steps |> List.filter (fun (l,_)->l=level) |> List.iter (snd>>(executeStep wc)))
    

  let run (repo:string) (product:string) (version:SemVersion option) (target:string) (simulate:bool) kill start uuid =
    let version = version |> Option.defaultValue (RepoClient.latest_version repo product)
    let versiondetails = RepoClient.version_details repo product version

    let expectations = versiondetails.Manifest.Expectations
    let steps = check_expectations target expectations kill start uuid repo product (version.ToString())



    if simulate 
    then render steps
    else execute steps

    0
      

