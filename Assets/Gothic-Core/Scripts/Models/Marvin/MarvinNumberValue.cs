namespace Gothic.Core.Models.Marvin
{
    /// <summary>
    /// Free input: non-negative integer within [min, max].
    /// </summary>
    public class MarvinNumberValue : IMarvinValueProvider
    {
        private readonly int _min;
        private readonly int _max;
        private readonly int _maxDigits;

        public string Placeholder { get; }
        public bool IsClosedSet => false;

        public MarvinNumberValue(string placeholder, int min, int max)
        {
            Placeholder = placeholder;
            _min = min;
            _max = max;
            _maxDigits = max.ToString().Length;
        }

        public MarvinPrefixIndex GetCandidates()
        {
            return null;
        }

        public bool IsValid(string token)
        {
            return TryParse(token, out var number) && number >= _min && number <= _max;
        }

        public bool IsValidPrefix(string partial)
        {
            return partial.Length == 0 || (TryParse(partial, out var number) && number <= _max);
        }

        private bool TryParse(string token, out int number)
        {
            number = 0;
            if (token.Length == 0 || token.Length > _maxDigits)
                return false;

            foreach (var c in token)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return int.TryParse(token, out number);
        }
    }
}
