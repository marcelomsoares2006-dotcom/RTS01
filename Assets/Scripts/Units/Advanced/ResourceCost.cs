using System;

namespace EraImperial.Units.Advanced
{
    /// <summary>
    /// Custo de produção. O recurso é guardado por nome ("Gold", "Wood", "Stone") para este assembly não
    /// depender do InGameResource do Assembly-CSharp; a ponte converte para ResourceValue.
    /// </summary>
    [Serializable]
    public struct ResourceCost
    {
        public static readonly string[] ValidResources = { "Gold", "Wood", "Stone" };

        public string resource;
        public int amount;

        public ResourceCost(string resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }

        public bool IsValid => Array.IndexOf(ValidResources, resource) >= 0 && amount > 0;
    }
}
