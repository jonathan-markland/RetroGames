﻿module ScreenGameOver

open DrawingShapes
open DrawingFunctions
open ResourceIDs
open ScoreHiScore
open Geometry
open ImagesAndFonts
open ScreenHandler
open Time

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type GameOverScreenModel =
    {
        ScoreAndHiScore : ScoreAndHiScore
        HiScoreText     : string
        WhereAfter      : ScoreAndHiScore -> float32<seconds> -> ErasedGameState
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let RenderGameOverScreen render (model:GameOverScreenModel) gameTime =

    Rectangle render 0<epx> 0<epx> ScreenWidthInt ScreenHeightInt (SolidColour(0x000000u))

    Text render GreyFontID CentreAlign MiddleAlign (ScreenWidthInt / 2) (ScreenHeightInt / 2) "GAME OVER"
    Text render GreyFontID CentreAlign MiddleAlign (ScreenWidthInt / 2) (125<epx>) (model.HiScoreText)

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NextGameOverScreenState gameState keyStateGetter gameTime _elapsed =

    //let model = ModelFrom gameState
    //    let input = keyStateGetter |> DecodedInput
    //
    //    if input.Fire.JustDown then
    //        model.WhereAfter model.ScoreAndHiScore gameTime
    //    else
        gameState |> Unchanged
    
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NewGameOverScreen scoreAndHiScore whereAfter =

    let gameOverModel =
        {
            ScoreAndHiScore = scoreAndHiScore
            HiScoreText     = "HI SCORE   " + scoreAndHiScore.HiScore.ToString()
            WhereAfter      = whereAfter
        }

    NewGameState NextGameOverScreenState RenderGameOverScreen gameOverModel
