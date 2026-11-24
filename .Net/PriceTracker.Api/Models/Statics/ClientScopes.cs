namespace PriceTracker.Back.Models.Statics
{
    //TODO: change name of scopes and values
    public static class ClientScopes
    {
        public const string ScopeClaim = "scope";

        //the project
        public const string CkL1 = "CkL1";

        //meta
        public const string CkL2 = "CkL2";

        //client in center
        public const string CkL3 = "CkL3";

        //scope for external services
        public const string CkL4 = "CkL4";

        //multiple
        public const string CkL12 = "CkL1,CkL2";
        public const string CkL13 = "CkL1,CkL3";
        public const string CkL14 = "CkL1,CkL4";



        public static List<ClaimScopeDTO> GetScopes()
        {
            var scopeList = new List<ClaimScopeDTO>()
            {
                //singles
                new (nameof(CkL1),[CkL1]),
                new (nameof(CkL2),[CkL2]),
                new (nameof(CkL3), [CkL3]),
                new (nameof(CkL4), [CkL4]),

                //multiple
                new (nameof(CkL12),[..CkL12.Split(',')]),
                new (nameof(CkL13),[..CkL13.Split(',')]),
                new (nameof(CkL14),[..CkL14.Split(',')]),
            };

            return scopeList;
        }
    }

    public class ClaimScopeDTO
    {
        public ClaimScopeDTO(string name, List<string> values)
        {
            Name = name;
            Values = values;
        }

        public string Name { get; set; }
        public List<string> Values { get; set; }
    }
}
