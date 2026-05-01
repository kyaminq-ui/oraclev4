using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PassivePool", menuName = "Oracle/Passive Pool")]
public class PassivePool : ScriptableObject
{
    [Header("Tous les passifs disponibles")]
    public List<PassiveData> allPassives = new List<PassiveData>();

    public List<PassiveData> GetRandom(int count)
    {
        var pool = new List<PassiveData>(allPassives);
        var result = new List<PassiveData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }
}
