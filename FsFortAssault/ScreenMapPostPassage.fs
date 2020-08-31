﻿module ScreenMapPostPassage

open Angle
open Time
open Rules
open SharedDrawing
open DrawingCommandsEx
open ScoreHiScore
open Geometry
open ImagesAndFonts
open ScorePanel
open MapScreenSharedDetail
open StoryboardChapterChange


let DefaultAlliedFleetLocation = { ptx=124.0F<epx> ; pty=75.0F<epx> }

/// These are permitted to overlap other rectangles, including the trigger rectangles.
let PermissableTravelLocationRectangles =
    [
        {
            Left   =  86.0F<epx>
            Top    =  75.0F<epx>
            Right  = 276.0F<epx>
            Bottom = 139.0F<epx>
        }
    ]

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type AlliedState =
    | AlliedFleetInPlay of PointF32
    | Paused            of PointF32 * timeOfEngagement:float32<seconds>
    | PostPassageScreenOver

type MapPostPassageScreenModel =
    {
        ScoreAndHiScore:      ScoreAndHiScore
        ShipsThrough:         uint32
        AlliedState:          AlliedState
        EnemyFleetCentre:     PointF32
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let AlliesVersusEnemy alliesLocation enemyLocation gameTime =

    if alliesLocation |> IsWithinRegionOf enemyLocation EnemyEngagementDistance then
        Paused(alliesLocation, gameTime)
    else
        AlliedFleetInPlay(alliesLocation)

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let RenderMapPostPassageScreen render (model:MapPostPassageScreenModel) =

    Image1to1 render 0<epx> 0<epx> ImageMap.ImageID

    // PermissableTravelLocationRectangles |> List.iteri (fun i r ->
    //     render (DrawFilledRectangle(r.Left, r.Top, r |> RectangleWidth, r |> RectangleHeight, i |> AlternateOf 0xEE0000u 0x00FF00u)))

    match model.AlliedState with
        | AlliedFleetInPlay(location)
        | Paused(location, _) ->
            CentreImage render location.ptx location.pty ImageAlliedFleetSymbol
            CentreImage render model.EnemyFleetCentre.ptx model.EnemyFleetCentre.pty ImageEnemyFleetSymbol
        | PostPassageScreenOver ->
            ()

    let h = ImageMap.ImageHeight |> FloatEpxToIntEpx

    ScoreboardArea render h

    let scorePanel =
        {
            ScoreAndHiScore  = model.ScoreAndHiScore
            ShipsPending     = 0u
            ShipsThrough     = model.ShipsThrough
            Tanks            = model.ShipsThrough |> ToTankCountFromShipCount
            Damage           = 0u
            Ammunition       = 10u
            Elevation        = 0.0F<degrees>
        }

    DrawScorePanel render h scorePanel

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NewMapPostPassageScreen scoreAndHiScore shipsThrough =
    {
        ScoreAndHiScore  = scoreAndHiScore
        ShipsThrough     = shipsThrough
        AlliedState      = AlliedFleetInPlay(DefaultAlliedFleetLocation)
        EnemyFleetCentre = DefaultEnemyFleetLocation
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NextMapPostPassageScreenState oldState input gameTime =

    let newModel =
        match oldState.AlliedState with

            | AlliedFleetInPlay(alliedLocation) ->
    
                let alliedLocation = NewAlliedFleetLocation alliedLocation input PermissableTravelLocationRectangles
                let enemyLocation = NewEnemyFleetLocation oldState.EnemyFleetCentre alliedLocation
                let allies = AlliesVersusEnemy alliedLocation enemyLocation gameTime

                {
                    ScoreAndHiScore  = oldState.ScoreAndHiScore
                    ShipsThrough     = oldState.ShipsThrough
                    AlliedState      = allies
                    EnemyFleetCentre = enemyLocation
                }

            | Paused(_,engagementTime) ->

                let elapsedSinceEngagement = gameTime - engagementTime
                if elapsedSinceEngagement > PauseTimeOnceEngaged then
                    { oldState with AlliedState = PostPassageScreenOver }
                else
                    oldState

            | PostPassageScreenOver ->
        
                oldState   // Ideology:  Never risk the logic rest of the logic when the screen is over.

    match newModel.AlliedState with
        
        | PostPassageScreenOver ->
            GoToNextChapter1(newModel)

        | _ -> 
            StayOnThisChapter1(newModel)
