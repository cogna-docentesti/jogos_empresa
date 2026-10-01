namespace Game.Infrastructure
{
    /// <summary>
    /// Nome de cada cena do build, num lugar so.
    ///
    /// Antes, "GameScene" e "0_Identification" estavam escritos como texto
    /// solto em mais de um script. Um erro de digitacao so aparecia em
    /// runtime, como tela preta. Com a constante, o compilador acusa na hora.
    ///
    /// Ao adicionar uma cena aqui, adicione tambem em
    /// File > Build Profiles > Scene List (antigo Build Settings).
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Cadastro do aluno (nome, RA, nome do restaurante). Primeira cena do build.</summary>
        public const string Identification = "0_Identification";

        /// <summary>
        /// Cena unica do jogo. As telas D1, D2, D3, hub etc. sao paineis
        /// dentro dela, ligados e desligados pelo UIStateListener.
        /// </summary>
        public const string Game = "GameScene";
    }
}
