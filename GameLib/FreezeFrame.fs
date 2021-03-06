﻿/// TODO: Reconsider this module since ScreenHandler.FrozenInTimeAt and ScreenHandler.WithoutAnyFurtherUpdates
module FreezeFrame

open Time
open GameStateManagement



type private FreezeFrameModel =
    {
        FrozenGameState   : ErasedGameState
        TimeFiddler       : GameTime -> GameTime
        UnfreezeGameTime  : GameTime
        PostFreezeCtor    : ErasedGameState -> GameTime -> ErasedGameState
    }



let private RenderFreezeFrame render model (gameTime:GameTime) =
    
    model.FrozenGameState.Draw render (gameTime |> model.TimeFiddler)



let private NextFreezeFrameState gameState _keyStateGetter gameTime _elapsed =
    
    let model = ModelFrom gameState
    
    if gameTime < model.UnfreezeGameTime then
        Unchanged gameState
    else
        model.PostFreezeCtor (model.FrozenGameState |> WithoutAnyFurtherUpdates) gameTime



let AdaptedToIgnoreOutgoingStateParameter (func:GameTime -> ErasedGameState) =
    let adapter (_outgoingState:ErasedGameState) (gameTime:GameTime) =
        func gameTime
    adapter



// TODO: Review the need for the following since ScreenHandler.FrozenInTimeAt and ScreenHandler.WithoutAnyFurtherUpdates



/// The outgoingGameState is frozen at the current gameTime.
/// The returned ErasedGameState will hold that image for the duration specified
/// before calling the 'whereToAfter' constructor, quoting the most recent
/// state, to establish the caller's desired next state.
let WithFreezeFrameFor duration gameTime whereToAfter outgoingGameState =

    let freezeModel =
        {
            FrozenGameState  = outgoingGameState
            TimeFiddler      = (fun _ -> gameTime)   // Freeze time for the outgoingGameState at the time right now.
            UnfreezeGameTime = gameTime + duration
            PostFreezeCtor   = whereToAfter
        }

    NewGameState NextFreezeFrameState RenderFreezeFrame freezeModel



/// An artistic alternative to just freezing the frame.
/// The outgoingGameState will see no further state update calls, but its drawing
/// function will be called with elapsing time for the duration specified.
let WithDrawingOnlyFor duration gameTime whereToAfter outgoingGameState =

    let freezeModel =
        {
            FrozenGameState  = outgoingGameState
            TimeFiddler      = id
            UnfreezeGameTime = gameTime + duration
            PostFreezeCtor   = whereToAfter
        }

    NewGameState NextFreezeFrameState RenderFreezeFrame freezeModel



            
    