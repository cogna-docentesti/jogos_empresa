using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStateMachine
{
    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnStateChanged;

    private readonly Dictionary<GameState, List<GameState>> allowedTransitions;

    public GameStateMachine()
    {
        CurrentState = GameState.Bootstrap;

        allowedTransitions = new Dictionary<GameState, List<GameState>>
        {
            { GameState.Bootstrap, new List<GameState> { GameState.MainMenu } },

            { GameState.MainMenu, new List<GameState>
                {
                    GameState.Config_Location,
                    GameState.Management_Hub
                }
            },

            { GameState.Config_Location, new List<GameState> { GameState.Config_Restaurant } },
            { GameState.Config_Restaurant, new List<GameState> { GameState.Config_TargetSegment } },
            { GameState.Config_TargetSegment, new List<GameState> { GameState.Config_Review } },

            { GameState.Config_Review, new List<GameState> { GameState.Initial_Equipment } },
            { GameState.Initial_Equipment, new List<GameState> { GameState.Initial_Team } },
            { GameState.Initial_Team, new List<GameState> { GameState.Initial_Capital } },
            { GameState.Initial_Capital, new List<GameState> { GameState.Management_Hub } },

            { GameState.Management_Hub, new List<GameState>
                {
                    GameState.Round_Start,
                    GameState.FinalReport,
                    GameState.GameOver_Bankruptcy
                }
            },

            { GameState.Round_Start, new List<GameState>
                {
                    GameState.Round_Sales,
                    GameState.GameOver_Bankruptcy
                }
            },

            { GameState.Round_Sales, new List<GameState> { GameState.Round_Costs } },
            { GameState.Round_Costs, new List<GameState> { GameState.Round_Event } },
            { GameState.Round_Event, new List<GameState> { GameState.Round_Summary } },

            { GameState.Round_Summary, new List<GameState>
                {
                    GameState.Management_Hub,
                    GameState.FinalReport,
                    GameState.GameOver_Bankruptcy
                }
            },

            { GameState.FinalReport, new List<GameState> { GameState.MainMenu } },
            { GameState.GameOver_Bankruptcy, new List<GameState> { GameState.MainMenu } }
        };
    }

    public bool CanTransitionTo(GameState nextState)
    {
        return allowedTransitions.ContainsKey(CurrentState)
            && allowedTransitions[CurrentState].Contains(nextState);
    }

    public bool TryChangeState(GameState nextState)
    {
        if (!CanTransitionTo(nextState))
        {
            Debug.LogWarning($"Transição inválida: {CurrentState} -> {nextState}");
            return false;
        }

        CurrentState = nextState;

        Debug.Log($"Estado atual: {CurrentState}");

        OnStateChanged?.Invoke(CurrentState);

        return true;
    }

    public void ForceState(GameState state)
    {
        CurrentState = state;

        Debug.Log($"Estado forçado: {CurrentState}");

        OnStateChanged?.Invoke(CurrentState);
    }
}