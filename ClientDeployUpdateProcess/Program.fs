namespace ClientDeploy

module CommandLineApp =

  open Argu
  open System

  type Arguments =
    | Install of string
    | Simulate
    | Check of string
    | Repository of string
    | Product of string
    | Read of ReadArgument
    | Version of string
    | Kill of int
    | Start of string
    | Args of string
    | UUID of string
  with
    interface IArgParserTemplate with
      member s.Usage =
        match s with
        | Install _ -> "install to specified target folder"
        | Simulate -> "simulate the installation and report steps"
        | Check _ -> "instruct ClientDeploy to check whether there is an update available"
        | Repository _ -> "specify a ClientDeploy repository base, format http://..."
        | Product _ -> "specify product channel name"
        | Read _ -> "read information on latest version"
        | Version _ -> "version for installation"
        | Kill _ -> "kill process with pid"
        | Start _ -> "start process after installation"
        | Args _ -> "Arguments for start process after installation"
        | UUID _ -> "relate installation"

  and ReadArgument = | Version | Releasenotes


  [<EntryPoint>]
  let main argv = 

      let parser = ArgumentParser.Create<Arguments>(programName = "ClientDeployUpdateProcess.exe")
 
      try
        let arguments = parser.Parse argv

        let repo = arguments.GetResult<@ Repository @>
        let product = arguments.GetResult<@ Product @>

        if arguments.Contains<@ Install @> 
        then 
          let targetfolder = arguments.GetResult<@ Install @>
          let version : (Semver.SemVersion) option = 
            if arguments.Contains<@ Arguments.Version @> 
            then Some (Semver.SemVersion.Parse (arguments.GetResult<@ Arguments.Version @>))
            else None
          let kill = if arguments.Contains<@ Kill @> then (Some (arguments.GetResult<@ Kill @>)) else None 
          let start = 
            if arguments.Contains<@ Start @> 
            then 
              let cargs = if arguments.Contains<@ Args @> then arguments.GetResult<@ Args @> else ""
              (Some ((arguments.GetResult<@ Start @>),cargs)) 
            else None 
          let uuid = if arguments.Contains<@ UUID @> then ((arguments.GetResult<@ UUID @>)) else System.Guid.NewGuid().ToString()
          Installer.run repo product version targetfolder (arguments.Contains<@ Simulate @>) kill start uuid


        elif arguments.Contains<@ Read @> 
        then
          let version = RepoClient.latest_version_information repo product
          match arguments.GetResult<@ Read @> with
          | Version -> printfn "%s" version.Version
          | Releasenotes -> printfn "%s" version.ReleaseNotes
          0

        elif arguments.Contains<@ Check @>
        then
          let action = arguments.GetResult<@ Check @> |> Semver.SemVersion.Parse
          let version = RepoClient.latest_version repo product

          if (version = action) 
            then 
              printfn "#LATEST"
              0
            else 
              printfn "#UPDATE\r\n%s" (version.ToString())
              1
        else          
          printfn "#USAGE"
          printfn "%s" (parser.PrintUsage())
          2
      with
      | :? ArguParseException as ex ->
        printfn "#ERROR\r\n\r\n%s" ex.Message
        2
      | :? RepoClient.TransientError as ex ->
        printfn "#ERROR\r\n\r\n%A" ex
        2
      | :? RepoClient.IncompatibleRepository as ex ->
        printfn "#ERROR\r\n\r\n%A" ex
        2
      | ex ->
        printfn "#ERROR\r\n\r\n%s" ex.Message
        2
