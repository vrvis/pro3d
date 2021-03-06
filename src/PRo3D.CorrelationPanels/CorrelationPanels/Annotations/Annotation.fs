namespace CorrelationDrawing

open System

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text
open Aardvark.UI
open Aardvark.SceneGraph

open GUI.CSS

open PRo3D.Base.Annotation

open CorrelationDrawing
open CorrelationDrawing.Types
open CorrelationDrawing.SemanticTypes
open CorrelationDrawing.AnnotationTypes

type MContactsTable  = amap<ContactId, AdaptiveContact>

type MAnnotationsTable = HashMap<ContactId, AdaptiveContact>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Contact =
  
  let getElevation (queryPoint : V3d) : float =
      queryPoint.Length

  let createNewContact (t : Geometry) : Contact = 
      {     
          id              = ContactId.ContactId (Guid.NewGuid())
          semanticType    = SemanticType.Undefined
          geometry        = t
          semanticId      = CorrelationSemanticId.invalid
          elevation       = getElevation
          points          = IndexList.empty
          
          projection      = Projection.Viewpoint
          visible         = true
          text            = ""          
          
          selected        = false
          hovered         = false
      }

  type Action =
      | SetSemantic     of option<CorrelationSemanticId>
      | ToggleSelected  of V3d
      | Select          of V3d 
      | HoverIn         of ContactId
      | HoverOut        of ContactId
      | Deselect

  let getColor (anno : option<AdaptiveContact>) (semanticApp : AdaptiveSemanticsModel) = 
        match anno with //TODO refactor
          | Some (a : AdaptiveContact) -> SemanticApp.getColor semanticApp a.semanticId                            
          | None -> AVal.constant C4b.Black
    
  let getColor' (anno : AdaptiveContact) (semanticApp : AdaptiveSemanticsModel) =          
    let rval =
      adaptive {
        return! SemanticApp.getColor semanticApp anno.semanticId
      }
    rval

  let getColor'' (imAnno : aval<Option<AdaptiveContact>>) (semanticApp : AdaptiveSemanticsModel) =
    adaptive {
      let! optAnno = imAnno
      let (a : aval<C4b>, b : aval<bool>) = 
          match optAnno with
            | Some an -> 
              let col = SemanticApp.getColor semanticApp an.semanticId                
              (col, an.hovered)
            | None -> ((AVal.constant C4b.Red), (AVal.constant false))

      // let! hover = b
      let! col = a
//      return match hover with
//              | true -> C4b.Yellow
//              | false -> col
      return col
    }

  let update  (a : Action) (anno : Contact) =
    match a with
        | SetSemantic str -> match str with
                              | Some s -> {anno with semanticId = s}
                              | None -> anno
        | ToggleSelected (point) -> 
          let ind =       
            anno.points.FirstIndexOf(fun (p : ContactPoint) -> Vec.AllEqual(p.point,point)) 
          match ind with
            | -1 -> anno
            | _  ->
              let setTo = not (anno.points.Item ind).selected
              let deselected =
                anno.points |> IndexList.map (fun p -> {p with selected = false})
              let upd = 
                deselected.Update  
                  (ind, (fun (p : ContactPoint) -> {p with selected = setTo}))
              {anno with points = upd}
        | Select (point) ->
          let ind =       
            anno.points.FirstIndexOf(fun (p : ContactPoint) -> Vec.AllEqual(p.point,point)) 
          let upd = anno.points.Update (ind, (fun (p : ContactPoint) -> {p with selected = true}))
          {anno with points         = upd}
        | HoverIn    id  -> 
          match (id = anno.id) with
            |  true -> 
              {anno with  hovered        = true }
            | false -> anno
        | HoverOut   id  ->
          match (id = anno.id) with
            | true -> {anno with hovered = false }
            | false -> anno
        | Deselect -> {anno with points = anno.points |> IndexList.map (fun p -> {p with selected = false})}


  let getSelected (anno : Contact) =
    let sel = 
      anno.points.Filter (fun i p -> p.selected)
        |> IndexList.tryAt 0
    sel |> Option.map (fun s -> (s, anno))

  let isSelected (anno : Contact) =
    (getSelected anno).IsSome


  module View = 
      let viewSelected (model : AdaptiveContact)  (semanticApp : AdaptiveSemanticsModel) = 
          let semanticsNode = 
              let iconAttr =
                  amap {
                      yield clazz "circle outline icon"
                      let! c = SemanticApp.getColor semanticApp model.semanticId
                      yield style (sprintf "color:%s" (GUI.CSS.colorToHexStr c))
                  }      
              td [clazz "center aligned"; style lrPadding] [
                  Incremental.i (AttributeMap.ofAMap iconAttr) (AList.ofList []);
                  Incremental.text (SemanticApp.getLabel semanticApp model.semanticId)
              ]
                      
          let geometryTypeNode =
            td [clazz "center aligned"; style lrPadding] 
               //[label  [clazz "ui label"] 
                       [text (model.geometry.ToString())]
          
          let projectionNode =
            td [clazz "center aligned"; style lrPadding]
               //[label  [clazz "ui label"] 
                       [text (model.projection.ToString())]
          
          let annotationTextNode = 
              td [clazz "center aligned"; style lrPadding] 
                 //[label  [clazz "ui label"] 
                         [Incremental.text model.text]
          
          [semanticsNode;geometryTypeNode;projectionNode;annotationTextNode]
      
      let viewDeselected (model : AdaptiveContact)  (semanticApp : AdaptiveSemanticsModel) = 
          let semanticsNode = 
            let iconAttr =
              amap {
                yield clazz "circle icon"
                let! c = SemanticApp.getColor semanticApp model.semanticId
                yield style (sprintf "color:%s" (GUI.CSS.colorToHexStr c))
  //              yield attribute "color" "blue"
              }      
            td [clazz "center aligned"; style lrPadding] 
               [
                Incremental.i (AttributeMap.ofAMap iconAttr) (AList.ofList []);
                //label  [clazz "ui label"] 
                       Incremental.text (SemanticApp.getLabel semanticApp model.semanticId)]
          
            
          let geometryTypeNode =
            td [clazz "center aligned"; style lrPadding] 
               //[label  [clazz "ui label"] 
                       [text (model.geometry.ToString())]
          
          let projectionNode =
            td [clazz "center aligned"; style lrPadding] 
               //[label  [clazz "ui label"] 
                       [text (model.projection.ToString())]
          
          let annotationTextNode = 
              td [clazz "center aligned"; style lrPadding] 
                 //[label  [clazz "ui label"] 
                         [Incremental.text model.text]
          
          [semanticsNode;geometryTypeNode;projectionNode;annotationTextNode]
      
      
      let view  (model : AdaptiveContact)  (semanticApp : AdaptiveSemanticsModel) = 
        model.selected
          |> AVal.map (fun d -> 
              match d with
                | true  -> viewSelected model semanticApp
                | false -> viewDeselected model semanticApp)
 
  module Sg =
    let view (model : AdaptiveContact) (cam : aval<CameraView>) (semApp : AdaptiveSemanticsModel) (working : bool) =

      let annoPointToSg (point : AdaptiveContactPoint) (color : aval<C4b>) (weight : aval<float>) =  
        let weight = weight |> AVal.map (fun w -> w * 0.2)
        let trafo = (AVal.constant (Trafo3d.Translation(point.point))) //TODO dynamic
        let pickSg = 
           Sg.sphereWithEvents (AVal.constant C4b.White) weight 
                [
                  Sg.onClick (fun p -> ToggleSelected (point.point))
                  Sg.onEnter (fun _ -> HoverIn model.id)
                  Sg.onLeave (fun _ -> HoverOut model.id)
                ]
              |> (Sg.trafo trafo)
              |> Sg.depthTest (AVal.constant DepthTestMode.Never)
          
        point.selected 
          |> AVal.map (fun sel -> 
                        let col =
                          match sel with
                            | true -> (AVal.constant C4b.Yellow)   
                            | false -> color
                        Sg.sphereDyn col weight  
                                |> Sg.trafo(trafo))
          |> Sg.dynamic
          |> Sg.andAlso pickSg


      let color = getColor' model semApp
      let thickness = SemanticApp.getThickness semApp model.semanticId
      let lines = 
          Sg.Incremental.polyline 
            (AList.map (fun (ap : AdaptiveContactPoint) -> ap.point) model.points)
            color
            thickness

      let dots =      
        let weight = (thickness |> AVal.map  (fun x -> x * 0.1))
        alist {
          let! count = (AList.count model.points)
          let last = count - 1
          let mutable i = 0
          for p in model.points  do
            if working then 
              let (col, weight) = 
                match  (i = last) with
                  | true -> 
                    let c = (AVal.constant C4b.Yellow )
                    let w = (thickness |> AVal.map  (fun x -> x * 0.2))
                    (c,w)
                  | false -> 
                    (color, weight)
              yield annoPointToSg p col weight // (computeScale view (p.point) 5.0) 
              i <- i + 1
            else
              yield annoPointToSg p color weight // (computeScale view (p.point) 5.0) 
        } 
        |> ASet.ofAList
        |> Sg.set

      [
       Sg.noEvents <| lines
       Sg.noEvents <| dots
      ] |> ASet.ofList

  let getThickness (anno : aval<Option<AdaptiveContact>>) (semanticApp : AdaptiveSemanticsModel) = 
    AVal.bind (fun (a : Option<AdaptiveContact>)
                  -> match a with
                      | Some a -> SemanticApp.getThickness semanticApp a.semanticId
                      | None -> AVal.constant CorrelationSemantic.ThicknessDefault) anno   

  let getThickness' (anno : AdaptiveContact) (semanticApp : AdaptiveSemanticsModel) = 
    SemanticApp.getThickness semanticApp anno.semanticId

//  let calcElevation (v : V3d) =
//    v.Y

//  let getAvgElevation (anno : MAnnotation) =
//    anno.points
//      |> AList.averageOf (calcElevation 
////
  let isElevationBetween' (f : V3d -> float) (v : V3d) (lower : V3d) (upper : V3d) =
      (f lower < f v) && (f upper > f v)

  let elevation (anno : Contact) =
    anno.points 
      |> IndexList.toList
      |> List.map (fun x -> anno.elevation x.point)
      |> List.average

  let lowestPoint (anno : Contact) = //TODO unsafe
    anno.points 
      |> DS.PList.minBy (fun x -> anno.elevation x.point)

  let tryLowestPoint (anno : Contact) =
    anno.points 
      |> DS.PList.tryMinBy (fun x ->anno.elevation x.point)

  let highestPoint (anno : Contact) = //TODO unsafe
    anno.points 
      |> DS.PList.maxBy (fun x -> anno.elevation x.point)

  let tryHighestPoint (anno : Contact) = 
    anno.points 
      |> DS.PList.tryMaxBy (fun x -> anno.elevation x.point)
  

  //let elevation' (anno : MAnnotation) =
  //  adaptive {
  //    let! lst = anno.points.Content
  //    return lst
  //            |> IndexList.map (fun x -> anno.elevation x.point)
  //            |> IndexList.toList
  //            |> List.average
  //  }

  let isElevationBetween (a : V3d) (b : V3d) (model : Contact)  =
      model.elevation a < (elevation model) 
        && (model.elevation b > (elevation model))
    
  let sortByElevation (p1 : V3d, a1 : Contact)  (p2 : V3d, a2 : Contact) =
    let (lp, la) = if a1.elevation p1 < a2.elevation p2 then (p1, a1) else (p2, a2) //TODO refactor
    let (up, ua) = if a1.elevation p1 < a2.elevation p2 then (p2, a2) else (p1, a1)
    ((lp,la),(up,ua))

  let getType (semanticApp : SemanticsModel) (anno : Contact) =
    let s = (SemanticApp.getSemantic semanticApp anno.semanticId)
    match s with
      | Some s -> s.semanticType
      | None   -> SemanticType.Undefined

  let getLevel (semanticApp : SemanticsModel) (anno : Contact) =
    //match anno.overrideLevel with
    //  | Some o -> o
    //  | None ->
        let s = SemanticApp.getSemantic semanticApp anno.semanticId
        match s with
          | Some s -> s.level
          | None   -> NodeLevel.invalid

  let getLevelById  (id : AnnotationTypes.ContactId) (contacts : ContactsTable) (semanticApp : SemanticsModel) =        
      let semantic = 
          contacts
          |> HashMap.tryFind id 
          |> Option.bind (fun a ->
              (SemanticApp.getSemantic semanticApp a.semanticId)
          )

      match semantic with
      | Some s -> s.level
      | None   -> NodeLevel.invalid

  let onlyHierarchicalAnnotations (semanticApp : SemanticsModel) (nodes : List<Contact>) =
    nodes
      |> List.filter (fun (a : Contact) -> 
      match (SemanticApp.getSemantic semanticApp a.semanticId) with
        | Some s  -> s.semanticType = SemanticType.Hierarchical
        | None    -> false)

  let splitByLevel (semanticApp : SemanticsModel) (annos : List<Contact>) =
    let sem (a : Contact) =  
      match (SemanticApp.getSemantic semanticApp a.semanticId) with
        | Some s -> s
        | None -> CorrelationSemantic.initInvalid //TODO something useful

    let levels = 
      annos 
        |> List.map (fun x -> (sem x).level) 
        |> List.distinct
      
    levels 
      |> List.map (fun (lvl : NodeLevel) ->
                    annos 
                      |> List.filter (fun a -> (sem a).level = lvl))

  let onlyLvli (semanticApp : SemanticsModel) (i : NodeLevel) (annos : List<Contact>) =
    annos
      |> List.filter (fun (a : Contact) -> 
      match (SemanticApp.getSemantic semanticApp a.semanticId) with
        | Some s  -> s.level = i
        | None    -> false)