﻿namespace PRo3D

open System
open Aardvark.Rendering
open Aardvark.UI
open PRo3D.Comparison
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.UI
open Aardvark.UI.Trafos
open Aardvark.UI.Primitives
open PRo3D.Core
open PRo3D.Base
open PRo3D.SurfaceUtils
open PRo3D.Core.Surface
open PRo3D.Base
open Chiron
open PRo3D.Base.Annotation
open Adaptify.FSharp.Core
open PRo3D.Comparison
open SurfaceMeasurements
open AreaSelection
open ComparisonUtils

type ComparisonAction =
  | SelectSurface1 of string
  | SelectSurface2 of string
  | UpdateAllMeasurements
  | UpdateCoordinateSystemMeasurements
  | UpdateAreaMeasurements
  | UpdateAnnotationMeasurements
  | ExportMeasurements of string
  | ToggleVisible
  | AddBookmarkReference of System.Guid
  | SetOriginMode of OriginMode
  | AddSelectionArea of V3d
  | UpdateSelectedArea of AreaSelectionAction
  | AreaSelectionMessage of System.Guid * AreaSelectionAction
  | SelectArea of System.Guid
  | DeselectArea
  | StopEditingArea
  | Nop

module CustomGui =
    let dynamicDropdown<'msg when 'msg : equality> (items    : list<aval<string>>)
                                                   (selected : aval<string>) 
                                                   (change   : string -> 'msg) =
        let attributes (name : aval<string>) =
            AttributeMap.ofListCond [
                Incremental.always "value" name
                onlyWhen (AVal.map2 (=) name selected) (attribute "selected" "selected")
            ]
     
        let callback = onChange (fun str -> 
                                    str |> change)

        select [callback; style "color:black"] [
                for name in items do
                    let att = attributes name
                    yield Incremental.option att (AList.ofList [Incremental.text name])
        ] 

    let surfacesDropdown (surfaces : AdaptiveSurfaceModel) (change : string -> 'msg) (noSelection : string)=
        let surfaceToName (s : aval<AdaptiveSurface>) =
            s |> AVal.bind (fun s -> s.name)

        let surfaces = surfaces.surfaces.flat |> toAvalSurfaces
        let surfaceNames = surfaces |> AMap.map (fun g s -> s |> surfaceToName)                                                         
                                    |> AMap.toAVal
        let items = 
          surfaceNames |> AVal.map (fun n -> n.ToValueList ())
            |> AVal.map (fun x -> List.append [(noSelection |> AVal.constant)] x)

        let dropdown = 
            items |> AVal.map (fun items -> dynamicDropdown items (noSelection |> AVal.constant) change)

        Incremental.div (AttributeMap.ofList []) (AList.ofAValSingle dropdown)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ComparisonApp =

    let init : ComparisonApp = {   
        showMeasurementsSg   = true
        originMode           = OriginMode.ModelOrigin
        surface1             = None
        surface2             = None
        surfaceMeasurements  = 
          {
              measurements1        = None
              measurements2        = None
              comparedMeasurements = None
          }
        annotationMeasurements = []     
        selectedArea = None
        isEditingArea = false
        areas = HashMap.empty
    }

    let updateAreaStatistics (m            : ComparisonApp) 
                             (surfaceModel : SurfaceModel) 
                             (refSystem    : ReferenceSystem) = 
        match m.surface1, m.surface2 with
        | Some s1, Some s2 ->
            let areas =   
              m.areas
                |> HashMap.map (fun g x -> updateAreaStatistic surfaceModel refSystem x s1)
                |> HashMap.filter (fun g x -> x.IsSome)
                |> HashMap.map (fun g x -> x.Value)
            areas
        | _,_ -> HashMap.empty

    let updateSurfaceMeasurements (m            : ComparisonApp) 
                                  (surfaceModel : SurfaceModel) 
                                  (refSystem    : ReferenceSystem) = 
        Log.line "[Comparison] Calculating coordinate system measurements..."
        let measurements1 = Option.bind (fun s1 -> updateSurfaceMeasurement 
                                                        surfaceModel refSystem m.originMode s1) 
                                        m.surface1 
        let measurements2 = Option.bind (fun s2 -> updateSurfaceMeasurement 
                                                        surfaceModel refSystem m.originMode s2)
                                        m.surface2
        let surfaceMeasurements = 
            {
                measurements1 = measurements1
                measurements2 = measurements2
                comparedMeasurements =
                    Option.map2 (fun a b -> SurfaceMeasurements.compare a b)
                                measurements1 measurements2
        
            }
        Log.line "[Comparison] Finished calculating coordinate system measurements..."
        surfaceMeasurements
        
    let updateAnnotationMeasurements (m            : ComparisonApp) 
                                     (surfaceModel : SurfaceModel) 
                                     (annotations  : HashMap<Guid, Annotation.Annotation>) 
                                     (bookmarks    : HashMap<Guid, Bookmark>)
                                     (refSystem    : ReferenceSystem) =
        Log.line "[Comparison] Calculating annotation measurements..."
        let annotationMeasurements =
            match m.surface1, m.surface2 with
            | Some s1, Some s2 ->
                AnnotationComparison.compareAnnotationMeasurements 
                                        s1 s2  annotations bookmarks
      
            | _,_ -> []
        Log.line "[Comparison] Finished calculating annotation measurements."
        annotationMeasurements
       

    let updateMeasurements (m            : ComparisonApp) 
                           (surfaceModel : SurfaceModel) 
                           (annotations  : HashMap<Guid, Annotation.Annotation>) 
                           (bookmarks    : HashMap<Guid, Bookmark>)
                           (refSystem    : ReferenceSystem) =
        Log.line "[Comparison] Calculating surface measurements..."
        let areas = updateAreaStatistics m surfaceModel refSystem

        let annotationMeasurements =
            updateAnnotationMeasurements m surfaceModel annotations bookmarks refSystem

        let surfaceMeasurements =
            updateSurfaceMeasurements m surfaceModel refSystem

        Log.line "[Comparison] Finished calculating surface measurements."
        {m with surfaceMeasurements    = surfaceMeasurements 
                annotationMeasurements = annotationMeasurements
                areas                  = areas
        }

    let updateArea (m            : ComparisonApp) 
                   (guid         : System.Guid)
                   (msg          : AreaSelectionAction) =
        let area = m.areas
                      |> HashMap.tryFind guid
        let m = 
            match area with
            | Some area -> 
                let area = AreaSelection.update area msg
                let areas =
                    HashMap.add guid area m.areas
                {m with areas = areas}
            | None -> m
        m //TODO rno

    let update (m            : ComparisonApp) 
               (surfaceModel : SurfaceModel) 
               (refSystem    : ReferenceSystem)
               (annotations  : HashMap<Guid, Annotation.Annotation>) 
               (bookmarks    : HashMap<Guid, Bookmark>)
               (msg          : ComparisonAction) =
        match msg with
        | UpdateAllMeasurements -> 
            let m = updateMeasurements m surfaceModel annotations bookmarks refSystem
            m , surfaceModel
        | UpdateCoordinateSystemMeasurements ->
            let surfaceMeasurements = updateSurfaceMeasurements m surfaceModel refSystem
            {m with surfaceMeasurements = surfaceMeasurements}, surfaceModel
        | UpdateAreaMeasurements ->
            let areas = updateAreaStatistics m surfaceModel refSystem
            {m with areas = areas} , surfaceModel
        | UpdateAnnotationMeasurements ->
            let annotationMeasurements = 
                updateAnnotationMeasurements m surfaceModel annotations bookmarks refSystem
            {m with annotationMeasurements = annotationMeasurements}, surfaceModel
        | SelectSurface1 str -> 
            let m = {m with surface1 = noSelectionToNone str}
            let m =
                match m.surface1, m.surface2 with
                | Some s1, Some s2 ->
                    updateMeasurements m surfaceModel annotations bookmarks refSystem
                | _,_ -> m
            m , surfaceModel
        | SelectSurface2 str -> 
            let m = {m with surface2 = noSelectionToNone str}
            let m =
                match m.surface1, m.surface2 with
                | Some s1, Some s2 ->
                    updateMeasurements m surfaceModel annotations bookmarks refSystem
                | _,_ -> m
            m , surfaceModel
        | ExportMeasurements filepath -> 
            m
              |> Json.serialize 
              |> Json.formatWith JsonFormattingOptions.Pretty 
              |> Serialization.writeToFile filepath
            Log.line "[Comparison] Measurements exported to %s" (System.IO.Path.GetFullPath filepath)
            m , surfaceModel
        | ComparisonAction.ToggleVisible ->
            let surfaceId1 = m.surface1 |> Option.bind (fun x -> findSurfaceByName surfaceModel x)
            let surfaceId2 = m.surface2 |> Option.bind (fun x -> findSurfaceByName surfaceModel x)
            let surfaceModel = toggleVisible surfaceId1 surfaceId2 surfaceModel
            m, surfaceModel
        | AddBookmarkReference bookmarkId ->
            m, surfaceModel
        | AddSelectionArea location ->
            let area = {AreaSelection.init (System.Guid.NewGuid ()) 
                          with location = location}
            let areas =
                HashMap.add area.id area m.areas
            
            {m with areas = areas
                    selectedArea = Some area.id
                    isEditingArea = true}, surfaceModel
        | UpdateSelectedArea msg ->
            match m.selectedArea with
            | Some guid ->
                updateArea m guid msg, surfaceModel
            | None ->
                Log.line "[ComparisonApp] No area selected."
                m, surfaceModel
        | AreaSelectionMessage (guid, msg) ->
            updateArea m guid msg, surfaceModel
        | SelectArea guid -> 
            {m with selectedArea = Some guid}, surfaceModel
        | DeselectArea -> 
            {m with selectedArea  = None
                    isEditingArea = false}, surfaceModel
        | StopEditingArea ->
            {m with isEditingArea = false}, surfaceModel

        | SetOriginMode originMode -> 
            let m = {m with originMode = originMode}
            let m = updateMeasurements m surfaceModel annotations bookmarks refSystem
            m, surfaceModel
        | Nop -> m, surfaceModel

    let isSelected (surfaceName : aval<string>) (m : AdaptiveComparisonApp) =
        let showSg = 
            AVal.map3 (fun (s1 : option<string>) s2 surfaceName -> 
                          match s1, s2 with
                          | Some s1, Some s2 ->
                            s1 = surfaceName || s2 = surfaceName
                          | Some s1, None -> s1 = surfaceName
                          | None, Some s2 -> s2 = surfaceName
                          | None, None -> false
                      ) m.surface1 m.surface2 surfaceName
        showSg

    let defaultCoordinateCross size trafo (origin : aval<V3d>) =
        let sg = 
            Sg.coordinateCross size
                |> Sg.trafo trafo
                |> Sg.noEvents
                |> Sg.effect [              
                    Shader.stableTrafo |> toEffect 
                    DefaultSurfaces.vertexColor |> toEffect
                ] 
                |> Sg.noEvents
                |> Sg.andAlso (
                    Sg.sphere 12 (C4b.Blue |> AVal.constant) 
                                 (size |> AVal.map (fun x -> x * 0.001)) 
                        |> Sg.trafo (origin |> AVal.map (fun x -> Trafo3d.Translation x))
                        |> Sg.noEvents
                        |> Sg.effect [              
                              Shader.stableTrafo |> toEffect 
                              DefaultSurfaces.vertexColor |> toEffect
                        ] 
                )
        sg

    let measurementsSg (surface     : aval<AdaptiveSurface>)
                       (size        : aval<float>)
                       (trafo       : aval<Trafo3d>) 
                       (referenceSystem : AdaptiveReferenceSystem)
                       (m           : AdaptiveComparisonApp) =    
        let surfaceName = surface |> AVal.bind (fun x -> x.name)
        let pivot = surface |> AVal.bind (fun x -> x.transformation.pivot)

       // let upDir = referenceSystem.up.value |> AVal.map (fun x -> x.Normalized)
      //  let northDir = referenceSystem.northO |> AVal.map (fun x -> x.Normalized)
      //  let east   =  AVal.map2 (fun (north : V3d) up -> north.Cross(up).Normalized) northDir upDir

        let showSg = isSelected surfaceName m

        let sg =
            showSg |> AVal.map (fun show -> 
                                  match show with
                                  | true -> defaultCoordinateCross size trafo pivot
                                  | false -> Sg.empty
                               )
        sg |> Sg.dynamic

    let view (m : AdaptiveComparisonApp) 
             (surfaces : AdaptiveSurfaceModel) =
        let measurementGui (name         : option<string>) 
                           (maesurements : option<SurfaceMeasurements>) =
            match name, maesurements with
            | Some name, Some maesurements -> 
                SurfaceMeasurements.view maesurements
            | _,_    -> 
                div [][]
                 

        let measurement1 = 
            (AVal.map2 (fun (s : option<string>) m -> 
                                measurementGui s m.measurements1) m.surface1 m.surfaceMeasurements)
                       |> AList.ofAValSingle


        let header surf = 
            (surf |> AVal.map (fun name -> 
                                      match name with
                                      | Some name -> sprintf "Measurements for %s"  name
                                      | None      -> "No surface selected"))

        let measurement2 = 
            (AVal.map2 (fun (s : option<string>) m -> 
                                measurementGui s m.measurements2
                       ) m.surface2 m.surfaceMeasurements)
                       |> AList.ofAValSingle

        let compared = 
            m.surfaceMeasurements
                |> (AVal.map (fun x -> 
                                  match x.comparedMeasurements with
                                  | Some m -> 
                                      SurfaceMeasurements.view m
                                  | None -> 
                                      div [] []
                             )
                    ) 

        let surfaceMeasurements =
            alist {
                yield div [] [Incremental.text (header m.surface1)]
                yield! measurement1
                yield div [] [Incremental.text (header m.surface2)]
                yield! measurement2
                yield div [] [text "Difference"]
                let! compared = compared
                yield compared
            }
        let surfaceMeasurements =
            Incremental.div (AttributeMap.ofList []) surfaceMeasurements
        //let header = sprintf "Measurements for %s"  name

        let surfaceMeasurements =
             AVal.map2 (fun (s1 : option<string>) s2 -> 
                            match s1, s2 with
                            | Some s1, Some s2 -> 
                                GuiEx.accordionWithOnClick "Surface Measurements"  "calculator" true [surfaceMeasurements] UpdateCoordinateSystemMeasurements
                            | _,_ -> GuiEx.accordion "Surface Measurements"  "calculator" true [] 
                       ) m.surface1 m.surface2
            
        //let measurement1 = 
        //    (AVal.bind2 (fun (s : option<string>) m -> 
        //                        measurementGui s (m.measurements1 
        //               ) m.surface1 m.surfaceMeasurements)
        //               |> AList.ofAValSingle

        //let measurement2 = 
        //    (AVal.bind2 (fun (s : option<string>) m -> 
        //                        measurementGui s (m.measurements2 |> AdaptiveOption.toOption)
        //               ) m.surface2 m.surfaceMeasurements)
        //               |> AList.ofAValSingle

        //let compared = 
        //    m.comparedMeasurements
        //        |> (AVal.map (fun m -> 
        //                          match m with
        //                          | AdaptiveOption.AdaptiveSome m -> 
        //                              SurfaceMeasurements.view m
        //                          | AdaptiveOption.AdaptiveNone -> 
        //                              div [] []
        //                     )
        //            ) 


        let updateButton =
          button [clazz "ui icon button"; onMouseClick (fun _ -> UpdateAllMeasurements )] 
                  [i [clazz "calculator icon"] []]  |> UI.wrapToolTip DataPosition.Bottom "Update"
        let exportButton = 
          button [clazz "ui icon button"
                  onMouseClick (fun _ -> ExportMeasurements "measurements.json")] 
                 [i [clazz "download icon"] [] ]
                    |> UI.wrapToolTip DataPosition.Bottom "Export"

        let annotationComparison =
            let tables = 
                adaptive {
                    let! s1 = m.surface1
                    let! s2 = m.surface2
                    let! measurements = m.annotationMeasurements
                    match s1, s2 with
                    | Some s1, Some s2 ->
                        let lst = 
                            alist {
                                for annoMeasurement in  measurements do
                                    yield (AnnotationComparison.view s1 s2 annoMeasurement)
                            }
                        let content = 
                            Incremental.div ([] |> AttributeMap.ofList) 
                                            lst
                        return content
                    | _,_ ->
                        return div [] []
                
                }
            let header = sprintf "Annotation Length Comparison"
            let content = Incremental.div  ([] |> AttributeMap.ofList) 
                                           (AList.ofAValSingle tables)
            let accordion =
                GuiEx.accordionWithOnClick header  "calculator" true [content] UpdateAnnotationMeasurements
            accordion

        let areaView =
            let selectedAreaView =
                alist {
                    let! guid = m.selectedArea
                    if guid.IsSome then
                        let area = AMap.find guid.Value m.areas
                        let! domNode =  (area |> AVal.map AreaSelection.view)
                        yield domNode
                }

            let header = sprintf "Area Comparison"
            let content = Incremental.div  ([] |> AttributeMap.ofList) 
                                           selectedAreaView
            let accordion =
                GuiEx.accordionWithOnClick header "calculator" true [content] UpdateAreaMeasurements
            accordion


        div [][
            br []
            div [clazz "ui buttons inverted"] 
                [updateButton;exportButton]
            br []
            Html.table [
              Html.row "Origin   " [Html.SemUi.dropDown m.originMode SetOriginMode]
            ]
            br []
            Html.table [
                Html.row "Surface1 " [CustomGui.surfacesDropdown surfaces SelectSurface1 noSelection]
                Html.row "Surface2 " [CustomGui.surfacesDropdown surfaces SelectSurface2 noSelection]
            ]
            br []
            div [] [areaView]
            br []
            Incremental.div ([] |> AttributeMap.ofList)  
                            (AList.ofAValSingle surfaceMeasurements)
            //GuiEx.accordion "Difference" "calculator" true [
            //   Incremental.div ([] |> AttributeMap.ofList)  (AList.ofAValSingle compared)
            //]
            br []
            annotationComparison
        ]