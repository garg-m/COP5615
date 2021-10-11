open System
//open System.IO
open System.Text;
open System.Diagnostics
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Akka.Configuration
open Akka.FSharp
//open type System.Math; 
open System.Collections.Generic 
open Akka.Actor

let system = System.create "my-system" <| ConfigurationFactory.Default()

type Message =
    | Stop
    | StartSum of int * string
    | FirstMessage of int
    | Tuple of float * float
    | Estimate

printfn "input ready"
let inputLine = Console.ReadLine() 
let splitLine = (fun (line : string) -> Seq.toList (line.Split ' '))
let inputParams = splitLine inputLine
let numOfNodes = inputParams.[0] |> int
let topology = inputParams.[1]
let alg = inputParams.[2]

let proc = Process.GetCurrentProcess()
let cpu_time_stamp = proc.TotalProcessorTime
let sw = Stopwatch.StartNew()

let mutable listOfActors = []//[0..inputParams.[0] |> int] // list of actors as long as the nodes inputted
//let mutable gridOfActors = [listOfActors]
let mutable cubeOfActors = []
let allActors = Map.empty

let find3dNeighbor (index: int list) = 
    (*let neighbor1 = [x-1,y,z]
    let neighbor2 = [x+1,y,z]
    let neighbor3 = [x,y-1,z]
    let neighbor4 = [x,y+1,z]
    let neighbor5 = [x,y,z-1]
    let neighbor6 = [x,y,z+1]*)
    
    // which axis to find neighbor on
    let random = Random()
    let mutable properIndexFound = false
    let mutable firstIndex = 0
    let mutable secondIndex = 0
    let mutable thirdIndex = 0

    while not properIndexFound do // loop until we are not index out of bounds
        let randomAxisIndex = random.Next(3)
        let mutable axisIndex = index.[randomAxisIndex] 
        let randomDirection = random.Next(2) // direction will either be 0 or 1 (forward/backward on axis)
        // which direction 
        if randomDirection = 1
        then axisIndex <- axisIndex + 1
        else axisIndex <- axisIndex - 1 
        let neighbor = index |> List.mapi (fun i v -> if i = randomAxisIndex then axisIndex else v) 
        firstIndex <- neighbor.[0]   
        secondIndex <- neighbor.[1]   
        thirdIndex <-  neighbor.[2] 
        let cubeLength = Math.Cbrt(numOfNodes |> float) |> int
        //printfn "index1: %d index2: %d index3: %d" randomDirection secondIndex thirdIndex
        if (firstIndex < cubeLength && firstIndex >= 0 && secondIndex < cubeLength && secondIndex >= 0 && thirdIndex < cubeLength && thirdIndex >= 0)
        then 
            properIndexFound <- true 
        else 
            properIndexFound <- false // i know this is pointless  

    let gridOfActors : _ list = cubeOfActors.[firstIndex] // first index as index into cube
    let rowOfActors : _ list = gridOfActors.[secondIndex]
    let actor = rowOfActors.[thirdIndex]
    actor
    //cubeOfActors.[neighbor.[0]][neighbor.[1]][neighbor.[2]] // return actor neighbor

let findLineNeighbor (index: int) = 
    
    // which axis to find neighbor on
    let random = Random()
    let mutable neighbor = index 
    let randomDirection = random.Next(1)

    // which direction 
    if randomDirection = 1
    then neighbor <- neighbor + 1
    else neighbor <- neighbor - 1 

    let actor = listOfActors.[neighbor]
    actor
    //cubeOfActors.[neighbor.[0]][neighbor.[1]][neighbor.[2]] // return actor neighbor

let gossipActor (neighbors: int[]) (mailbox : Actor<_>) (*(mailbox: Actor<_>)*) =

    let mutable counter = 0
    let rand = Random()
    rand.Next(0, neighbors.Length) |> ignore

    let rec loop () = 
       
       actor {
            let! msg = mailbox.Receive()
            let index = rand.Next(0, neighbors.Length) |> int  
            let target = neighbors.[index]
            //target <! msg
            counter <- counter + 1
            if counter < 50 then
                if counter = 1 then
                    mailbox.Context.Parent <! msg
                return! loop()
    }
    loop()

let pushSum (name:string) (topologyPosition:int list) = spawn system name <| fun mailbox ->
        //let mutable keepMessaging = true
        let rec loop(s,w, count,(position: int list)) = actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender() 
            let mutable localS = s
            let mutable localW = w
            let mutable counter = count
            let mutable position = position
            let random = Random()
            
            match msg with
            | FirstMessage numOfNodes -> 
                let randomNum = random.Next(numOfNodes) // randomly choose actor to send to
                localW <- localW/2.0 // keep half and send half
                localS <- localS/2.0
                match topology with
                    | "line" -> 
                        let neighborActor = findLineNeighbor(position.[0])  
                        //printfn "calling actor: %d" randomNum
                        neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                    | "3D" -> 
                        //printfn "calling actor @ %A" position 
                        let neighborActor = find3dNeighbor(position)
                        //system.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(5.), TimeSpan.FromSeconds(10.), neighborActor, ())
                        neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                    | "imp3D" -> 
                        let neighborActor = find3dNeighbor(position) 
                        neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                        printfn "imperfect 3D"
                    | _ -> 
                        printfn "here"
                        listOfActors.[randomNum] <! (Tuple(localS,localW)) // send half of s and w to next actor  
            | Tuple (recievedS,recievedW) -> 
                //printfn "local s: %f" localS
                //printfn "recieved s: %f" recievedS
                localW <- localW + recievedW
                localS <- localS + recievedS
                
                localS <- localS/2.0; // keep half
                localW <- localW/2.0;
                //printfn "len: %d" listOfActors.Length
                //printfn "local s again: %f " localS
                let randomNum = random.Next(numOfNodes) // randomly choose actor to send to

                if count < 3
                then
                    match topology with
                        | "line" -> 
                            let neighborActor = findLineNeighbor(position.[0])  
                            //printfn "calling actor: %d" randomNum
                            neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                        | "3D" -> 
                            let neighborActor = find3dNeighbor(position)
                            //printfn "calling actor @ %A" position 
                            neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                        | "imp3D" -> 
                            let neighborActor = find3dNeighbor(position) 
                            neighborActor <! (Tuple(localS,localW)) // send half of s and w to next actor
                        | _ -> listOfActors.[randomNum] <! (Tuple(localS,localW)) // send half of s and w to next actor         
            | Estimate ->   
                let estimate =  s/w 
                printfn "estimate: %f" estimate
            | _ -> printfn "wut" 
            
            let oldEstimate = s/w
            let newEstimate = localS/localW
            let diff = Math.Abs(newEstimate - oldEstimate)
            let minimumDiff = Math.Pow(10.0,-10.0)
            //printfn "actor: %s estimates: " name
            //printfn "old : %d" oldEstimate
            //printfn "new : %d" newEstimate
            if ( (diff) < minimumDiff )
            then counter <- counter + 1
            else
                //printfn "diff: %.10f" diff
                counter <- 0

            if counter > 2
            then 
                printfn "actor: %s terminating" name
                printfn "diff: %.10f" diff
                //keepMessaging <- false
                mailbox.Context.System.Terminate() |> ignore
           
            // handle an incoming message
            return! loop(localS,localW,counter,position) // store the new s,w into the next state of the actor
        }
        let initialS = name |> float
        //printfn "actor: %s is at initial position: %A" name topologyPosition
        loop(initialS,1.0,0,topologyPosition) // all actors start out with an s and w value that is maintained 

let addNodesInArray nodes = 
    for i in 1..nodes do 
        let name = i |> string
        let actor = [pushSum name [i]]
        listOfActors <- List.append listOfActors actor  // append 

let addNodesInCube nodes = 
    let cubeLength = Math.Cbrt(nodes |> float) |> int
    printfn "cube length %d" cubeLength
    for grid in 0..cubeLength-1 do 
        let mutable gridOfActors = [] // make a new grid
        let gridNum = grid |> string 
        for row in 0..cubeLength-1 do 
            let mutable rowOfActors = []
            let rowNum = row |> string
            for cell in 0..cubeLength-1 do
                let cellNum = cell |> string
                let actorName = gridNum + rowNum + cellNum
                //printfn "position: %s" actorName
                let actor = [pushSum actorName [grid;row;cell]]
                rowOfActors <- List.append rowOfActors actor  // append actor 
            let rowOfActors2 = [rowOfActors]
            gridOfActors <- List.append gridOfActors rowOfActors2 // append row of actors 
        
        let gridOfActors2 = [gridOfActors] 
        cubeOfActors <- List.append cubeOfActors gridOfActors2 // append grid for each layer of depth
    

let boss = 
    spawn system "boss" 
        (actorOf2 (fun mailbox msg ->
            match msg with  
            | StartSum (nodes, topology) -> 
                let random = Random()
                let randomNum = random.Next(nodes) // randomly choose actor to start with
                match topology with
                    | "line" -> 
                        addNodesInArray(numOfNodes) 
                        listOfActors.[randomNum] <! FirstMessage(nodes) // s = i, w = 1 
                    | "3D" -> 
                        printfn "3D topology"
                        addNodesInCube(numOfNodes)
                        let gridOfActors : _ list = cubeOfActors.[0] // first index as index into cube
                        let listOfActors : _ list = gridOfActors.[0]
                        let actor = listOfActors.[0]                        
                        actor <! FirstMessage(nodes)
                    | "imp3D" -> 
                        addNodesInCube(numOfNodes) 
                        cubeOfActors.[randomNum].[0].[0] <! FirstMessage(nodes)
                    | _ -> 
                        addNodesInArray(numOfNodes)  // append  
                        listOfActors.[randomNum] <! FirstMessage(nodes) // s = i, w = 1    
            | Stop -> mailbox.Context.System.Terminate() |> ignore
            | _ -> printfn "here"   
            
        ))
//printfn "%s" inputParams.[0] // should be number of nodes

if String.Equals(alg,"gossip",StringComparison.CurrentCultureIgnoreCase)
then boss <! StartSum (inputParams.[0] |> int, inputParams.[1]) // do gossip 
else boss <! StartSum (inputParams.[0] |> int, inputParams.[1]) // otherwise do sum


let input2 = System.Console.ReadLine() |> ignore
