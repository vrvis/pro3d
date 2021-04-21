namespace PRo3D.Core

open System
open FSharp.Data.Adaptive
open Adaptify
open Aardvark.Base
open Aardvark.UI
open PRo3D
open PRo3D.Base
open PRo3D.Core.Surface

open Chiron

open Aether
open Aether.Operators

#nowarn "0686"

type Orientation = 
| Horizontal = 0 
| Vertical   = 1 
| Sky        = 2

type Pivot = //alignment
| Left   = 0
| Middle = 1
| Right  = 2

type Unit =
| Undefined = 0
| mm        = 1
| cm        = 2
| m         = 3
| km        = 4

[<ModelType>]
type scSegment = {
    startPoint : V3d
    endPoint   : V3d
    color      : C4b
}
//with
//    static member FromJson ( _ : scSegment) =
//        json {

//            let! startPoint = Json.read "startPoint"
//            let! endPoint = Json.read "endPoint"
//            let! color  = Json.read "color"

//            return {
//                startPoint = startPoint |> V3d.Parse
//                endPoint   = endPoint |> V3d.Parse
//                color      = color |> C4b.Parse
//            }
//        }

//    static member ToJson ( x : scSegment) =
//        json {
//            do! Json.write "startPoint" (x.startPoint.ToString())
//            do! Json.write "endPoint" (x.endPoint.ToString())
//            do! Json.write "color" (x.color.ToString())
//        }

//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module scSegment = 

    let read0 =
        json {
            
            let! startPoint = Json.read "startPoint"
            let! endPoint = Json.read "endPoint"
            let! color  = Json.read "color"

            return {
                startPoint = startPoint |> V3d.Parse
                endPoint   = endPoint |> V3d.Parse
                color      = color |> C4b.Parse
            }
        }

type scSegment with
    static member FromJson ( _ : scSegment) =
        json {
            return! scSegment.read0
        }

    static member ToJson ( x : scSegment) =
        json {
            do! Json.write "startPoint" (x.startPoint.ToString())
            do! Json.write "endPoint" (x.endPoint.ToString())
            do! Json.write "color" (x.color.ToString())
        }

[<ModelType>]
type ScaleBar = {
    version         : int
    guid            : System.Guid
    name            : string

    text           : string
    textsize       : NumericInput
   
    isVisible       : bool
    position        : V3d    
    scSegments      : IndexList<scSegment>
    orientation     : Orientation
    alignment       : Pivot
    thickness       : NumericInput
    length          : NumericInput
    unit            : Unit //Formatting.Len //
    subdivisions    : NumericInput

    view            : CameraView
    transformation  : Transformations
    preTransform    : Trafo3d
}

[<ModelType>]
type ScaleBarDrawing = {
    orientation     : Orientation
    alignment       : Pivot
    thickness       : NumericInput
    length          : NumericInput
    unit            : Unit //Formatting.Len //
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ScaleBar =    
    
    let current = 0   
    let read0 =
        json {
            let! guid            = Json.read "guid"
            let! name            = Json.read "name"

            let! text     = Json.read "text"
            let! textSize = Json.readWith Ext.fromJson<NumericInput,Ext> "textsize"

            let! isVisible       = Json.read "isVisible"
            let! position        = Json.read "position"
            //let! scSegments      = Json.read "scSegments"

            let! orientation     = Json.read "orientation"
            let! alignment       = Json.read "alignment"
            let! thickness       = Json.readWith Ext.fromJson<NumericInput,Ext> "thickness"
            let! length          = Json.readWith Ext.fromJson<NumericInput,Ext> "length"
            let! unit            = Json.read "unit"
            let! subdivisions    = Json.readWith Ext.fromJson<NumericInput,Ext> "subdivisions"
            
            let! (view : list<string>) = Json.read "view"
            
            let view = view |> List.map V3d.Parse
            let view = CameraView(view.[0],view.[1],view.[2],view.[3], view.[4])

            let! transformation  = Json.read "transformation"
            let! preTransform    = Json.read "preTransform"

            return 
                {
                    version       = current
                    guid            = guid |> Guid
                    name            = name
                    
                    text            = text      
                    textsize        = textSize  

                    isVisible       = isVisible
                    position        = position |> V3d.Parse
                    scSegments      = IndexList.empty //scSegments  |> Serialization.jsonSerializer.UnPickleOfString
                    orientation     = orientation |> enum<Orientation>
                    alignment       = alignment   |> enum<Pivot>
                    thickness       = thickness     
                    length          = length   
                    unit            = unit |> enum<Unit> //Unit
                    subdivisions    = subdivisions

                    view            = view
                    transformation  = transformation
                    preTransform    = preTransform |> Trafo3d.Parse
                }
        }

type ScaleBar with
    static member FromJson(_ : ScaleBar) =
        json {
            let! v = Json.read "version"
            match v with 
            | 0 -> return! ScaleBar.read0
            | _ -> 
                return! v 
                |> sprintf "don't know version %A  of ScaleBar"
                |> Json.error
        }
    static member ToJson(x : ScaleBar) =
        json {
            do! Json.write "version" x.version
            do! Json.write "guid" x.guid
            do! Json.write "name" x.name

            do! Json.write "text"    x.text
            do! Json.writeWith (Ext.toJson<NumericInput,Ext>) "textsize" x.textsize

            do! Json.write "isVisible" x.isVisible    
            do! Json.write "position" (x.position.ToString())
            //do! Json.write "scSegments"  (x.scSegments |> IndexList.toList )
            do! Json.write "orientation" (x.orientation |> int)
            do! Json.write "alignment" (x.alignment |> int)
            do! Json.writeWith (Ext.toJson<NumericInput,Ext>) "thickness" x.thickness
            do! Json.writeWith (Ext.toJson<NumericInput,Ext>) "length" x.length
            do! Json.write "unit" (x.unit |> int)
            do! Json.writeWith (Ext.toJson<NumericInput,Ext>) "subdivisions" x.subdivisions

            let camView = x.view
            let camView = [camView.Sky; camView.Location; camView.Forward; camView.Up ; camView.Right] |> List.map(fun x -> x.ToString())      
            do! Json.write "view" camView
            do! Json.write "transformation" x.transformation  
            do! Json.write "preTransform" (x.preTransform.ToString())
        }


[<ModelType>]
type ScaleBarsModel = {
    version          : int
    scaleBars        : HashMap<Guid,ScaleBar>
    selectedScaleBar : Option<Guid> //Option<ScaleBar>
}

module ScaleBarsModel =
    
    let current = 0    
    let read0 = 
        json {
            let! scaleBars = Json.read "scaleBars"
            let scaleBars = scaleBars |> List.map(fun (a : ScaleBar) -> (a.guid, a)) |> HashMap.ofList

            let! selected     = Json.read "selectedScaleBar"
            return 
                {
                    version          = current
                    scaleBars        = scaleBars
                    selectedScaleBar = selected
                }
        }  
        
    let initial =
        {
            version          = current
            scaleBars        = HashMap.empty
            selectedScaleBar = None
        }

 
    
type ScaleBarsModel with
    static member FromJson (_ : ScaleBarsModel) =
        json {
            let! v = Json.read "version"
            match v with
            | 0 -> return! ScaleBarsModel.read0
            | _ ->
                return! v
                |> sprintf "don't know version %A  of ScaleBarsModel"
                |> Json.error
        }

    static member ToJson (x : ScaleBarsModel) =
        json {
            do! Json.write "version"           x.version
            do! Json.write "scaleBars"        (x.scaleBars |> HashMap.toList |> List.map snd)
            do! Json.write "selectedScaleBar"  x.selectedScaleBar
        }

module InitScaleBarsParams =

    let translationInput = {
        value   = 0.0
        min     = -10000000.0
        max     = 10000000.0
        step    = 0.01
        format  = "{0:0.00}"
    }

    let yaw = {
        value   = 0.0
        min     = -180.0
        max     = +180.0
        step    = 0.01
        format  = "{0:0.00}"
    }

    let initTranslation (v : V3d) = {
        x     = { translationInput with value = v.X }
        y     = { translationInput with value = v.Y }
        z     = { translationInput with value = v.Z }
        value = v    
    }

    let transformations = {
        version              = Transformations.current
        useTranslationArrows = false
        translation          = initTranslation (V3d.OOO)
        trafo                = Trafo3d.Identity
        yaw                  = yaw
        pivot                = V3d.Zero
    }

    let thickness = {
        value   = 0.03
        min     = 0.001
        max     = 1.0
        step    = 0.001
        format  = "{0:0.000}"
    }

    let length = {
        value   = 1.0
        min     = 0.0
        max     = 10000.0
        step    = 1.0
        format  = "{0:0}"
    }
    
    let text = {
        value   = 0.05
        min     = 0.01
        max     = 5.0
        step    = 0.01
        format  = "{0:0.00}"
    }

    let subdivisions = {
        value   = 5.0
        min     = 1.0
        max     = 1000.0
        step    = 1.0
        format  = "{0:0}"
    }

    let initialScaleBarDrawing = {
        orientation     = Orientation.Horizontal
        alignment       = Pivot.Left
        thickness       = thickness
        length          = length
        unit            = Unit.m //Formatting.Len length.value
    }

   


   