﻿module ScreenGameTitle

open DrawingCommandsEx
open ImagesAndFonts
open Geometry
open FontAlignment
open InputEventData
open BeachBackgroundRenderer
open Time
open GameGlobalState
open ScoreboardModel
open StaticResourceAccess

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// Intended to form a barrier against pressing FIRE 
/// repeatedly at the end of the Enter Your Name screen.
let TimeBeforeResponding = 2.0F<seconds>

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type GameTitleScreenState =
    | GameTitleAwaitingFireButton
    | GameTitleScreenOver

type GameTitleScreenModel =
    {
        ScreenStartTime : float32<seconds>
        GameGlobalState : GameGlobalState
        HiScore         : uint32
        State           : GameTitleScreenState
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let RenderGameTitleScreen render model (gameTime:float32<seconds>) =

    RenderBeachBackground render (gameTime / 4.0F)
    CentreImage render 160.0F<epx> 68.0F<epx> (ImageTitle |> ImageFromID)

    let scoreboardText = ScoreboardText 30 model.GameGlobalState.GameScoreBoard  // TODO: memoize in the model?
    Paragraph render BlackFontID CentreAlign MiddleAlign 160<epx> 94<epx> 20<epx> scoreboardText

    Text render BlackFontID CentreAlign MiddleAlign 160<epx> 180<epx> "USE CURSOR KEYS ... Z TO FIRE"

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NewGameTitleScreen hiScore gameGlobalState gameTime =
    {
        GameGlobalState = gameGlobalState
        HiScore         = hiScore
        State           = GameTitleAwaitingFireButton
        ScreenStartTime = gameTime
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NextGameTitleScreenState oldState input gameTime =

    if input.Fire.JustDown then

        let respondTime = oldState.ScreenStartTime + TimeBeforeResponding

        if gameTime > respondTime then
            { oldState with State = GameTitleScreenOver }

        else
            oldState

    else
        oldState

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Query functions for Storyboard
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let StayOnTitleScreen state =
    match state.State with
        | GameTitleAwaitingFireButton -> true
        | GameTitleScreenOver -> false

    