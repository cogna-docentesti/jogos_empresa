public enum GameState
{
    Bootstrap,
    MainMenu,

    Config_Location,
    Config_Restaurant,
    Config_TargetSegment,
    Config_Review,

    Initial_Equipment,
    Initial_Team,
    Initial_Capital,

    Management_Hub,

    Round_Start,
    Round_Sales,
    Round_Costs,
    Round_Event,
    Round_Summary,

    FinalReport,
    GameOver_Bankruptcy,

    // Append only: values 0-16 are already serialized in existing assets.
    Config_Identification = 17
}
