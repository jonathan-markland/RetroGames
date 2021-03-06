﻿module ScreenGameTitle

open GameStateManagement
open DrawingFunctions
open ResourceIDs
open Geometry
open ImagesAndFonts
open Time
open ScoreboardModel
open StaticResourceAccess
open GamePlayScreenConstants

let MainScreenSwitchRate = 0.125

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type private GameTitleScreenModel =
    {
        ScreenStartTime    : GameTime
        ScoreboardMemo     : string list
        MemoizedBackground : Image
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let private RenderGameTitleScreen render model (gameTime:GameTime) =

    Image1to1 render 0<epx> 0<epx> (model.MemoizedBackground)

    let x50pc = 50 |> PercentOfScreenWidth
    let y0 = 20 |> PercentOfScreenHeight
    let y1 = 30 |> PercentOfScreenHeight
    let y2 = 55 |> PercentOfScreenHeight
    let y3 = 65 |> PercentOfScreenHeight
    let y4 = 85 |> PercentOfScreenHeight

    let smallFont = FontFromID MissionIIFontID
    let scoreFont = MagnifiedFont  6  2 2  smallFont
    let bigFont   = MagnifiedFont  6  4 6  smallFont


    if PulseActiveAtRate MainScreenSwitchRate (gameTime - model.ScreenStartTime) then

        TextX render bigFont   CentreAlign MiddleAlign x50pc y1 "MISSION II"
        TextX render smallFont CentreAlign MiddleAlign x50pc y2 "A RETRO REMAKE OF THE BBC MICRO CLASSIC"
        TextX render smallFont CentreAlign MiddleAlign x50pc y3 "CYBERTRON MISSION"

    else

        let verticalSpacing = smallFont.CharHeight * 3<epx> + 4<epx>
        TextX render scoreFont CentreAlign MiddleAlign x50pc y0 "HI SCORES"
        ParagraphX render scoreFont CentreAlign MiddleAlign x50pc y2 verticalSpacing model.ScoreboardMemo


    TextX render smallFont CentreAlign MiddleAlign x50pc y4 "CONTROLS   CURSOR KEYS   Z FIRE"

        

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NewGameTitleScreen globalScoreboard gameTime =

    let titleScreenModel =
        {
            ScreenStartTime    = gameTime
            ScoreboardMemo     = ScoreboardText 24 globalScoreboard
            MemoizedBackground = PulseBetween 1.0 BackgroundImageID Background2ImageID gameTime |> ImageFromID
        }

    NewGameState ModelNeverChanges RenderGameTitleScreen titleScreenModel


