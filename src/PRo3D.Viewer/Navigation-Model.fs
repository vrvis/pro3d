namespace PRo3D.Navigation2

open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open PRo3D
open PRo3D.ReferenceSystem
open Aardvark.Base

[<ModelType>]
type NavigationModel = {
    camera         : CameraControllerState    
    navigationMode : NavigationMode      
    exploreCenter  : V3d
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NavigationModel =
    let initial = {
        camera = CameraController.initial          
        navigationMode =  NavigationMode.FreeFly        
        exploreCenter = V3d.Zero // make option        
        }
