/// <summary>
/// Type fonctionnel et visuel d'une cellule de la grille.
/// Détermine à la fois l'apparence (quel sprite) et le comportement (marchable ou non).
/// </summary>
public enum CellTileType
{
    Ground,         // Sol standard — marchable
    GroundBlood,    // Sol avec taches de sang — marchable, variante décorative
    GroundGrass,    // Sol avec herbe morte — marchable, variante décorative
    Obstacle,       // Rocher / ruine — NON marchable, bloque le passage et la LDV
    SpawnTeam1,     // Zone de placement initiale Équipe 1 — marchable
    SpawnTeam2,     // Zone de placement initiale Équipe 2 — marchable
}
