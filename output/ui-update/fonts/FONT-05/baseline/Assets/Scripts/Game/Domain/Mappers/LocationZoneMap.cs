/// <summary>
/// Traducao entre o id da area no mapa ("bank", "university"...) e a
/// LocationZone que vai para a sessao.
///
/// Fica num lugar so porque tres pontos precisam dela: a tela D1 (ao
/// confirmar e ao reidratar), o rascunho PlayerSession (ao carregar uma
/// sessao salva) e os testes.
///
/// Os assets LOC_* ja estao com o campo zone correto. Quando a E-06 trocar o
/// repositorio por SQLite e o Establishment passar a carregar a zona, esta
/// classe pode ser apagada.
/// </summary>
public static class LocationZoneMap
{
    public static LocationZone ToZone(string establishmentId) => establishmentId switch
    {
        "bank"        => LocationZone.Financas,
        "university"  => LocationZone.Educacao,
        "store"       => LocationZone.Comercio,
        "condominium" => LocationZone.Residencial,
        "marketing"   => LocationZone.Servicos,
        _             => LocationZone.Financas
    };

    public static string ToId(LocationZone zone) => zone switch
    {
        LocationZone.Financas    => "bank",
        LocationZone.Educacao    => "university",
        LocationZone.Comercio    => "store",
        LocationZone.Residencial => "condominium",
        LocationZone.Servicos    => "marketing",
        _                        => null
    };

    public static bool IsKnownId(string establishmentId) => establishmentId switch
    {
        "bank" or "university" or "store" or "condominium" or "marketing" => true,
        _ => false
    };
}
