﻿namespace ClientDeploy

module CommandLineApp =

  open Argu

  type Arguments =
    | Check of string
    | Repository of string
    | Product of string
  with
    interface IArgParserTemplate with
      member s.Usage =
        match s with
        | Check _ -> "instruct ClientDeploy to check whether there is an update available"
        | Repository _ -> "specify a ClientDeploy repository base, either http://... or file:///..."
        | Product _ -> "specify product channel name"


  [<EntryPoint>]
  let main argv = 

      let parser = ArgumentParser.Create<Arguments>(programName = "ClientDeployUpdateProcess.exe")
 
      try
        let arguments = parser.Parse argv

        if arguments.Contains<@ Check @>
        then
          let action = arguments.GetResult<@ Check @> |> Semver.SemVersion.Parse
          let repo = arguments.GetResult<@ Repository @>
          let product = arguments.GetResult<@ Product @>

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