using System.Collections.Generic;

namespace EraImperial.Units.Advanced
{
    /// <summary>Lista de problemas encontrados por uma validação. Vazia = válido.</summary>
    public sealed class ValidationReport
    {
        private readonly List<string> _problems = new List<string>();

        public IReadOnlyList<string> Problems => _problems;
        public bool IsValid => _problems.Count == 0;

        public void Add(string problem) { _problems.Add(problem); }

        public void AddRange(IEnumerable<string> problems) { _problems.AddRange(problems); }

        public override string ToString()
        {
            return IsValid ? "OK" : string.Join("; ", _problems);
        }
    }
}
