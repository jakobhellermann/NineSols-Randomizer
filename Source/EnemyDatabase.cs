using System;
using System.Collections.Generic;
using NineSolsAPI.Utils;

namespace Randomizer;

public record MonsterData(
    string Scene,
    string Name,
    string Path,
    bool Miniboss,
    bool Boss,
    bool Flying,
    bool Variant,
    bool Shielded) {
}

public class EnemyDatabase {
    public List<MonsterData> Monsters;

    public EnemyDatabase() {
        Monsters = AssemblyUtils.GetEmbeddedJson<List<MonsterData>>("Randomizer.monsters.json") ??
                   throw new InvalidOperationException("monsters could not be serialized");
    }
}