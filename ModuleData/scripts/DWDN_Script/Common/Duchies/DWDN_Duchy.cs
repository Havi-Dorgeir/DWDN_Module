using System;
using System.Collections.Generic;

namespace DWDN_Script.Common.Duchies
{
    /// <summary>
    /// Описание герцогства (Банрейх, Рецизия и др.)
    /// </summary>
    public class DWDN_Duchy
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BannerCode { get; set; }
        public bool IsEnabled { get; set; } = true;

        public List<DWDN_ClassDefinition> Classes { get; } = new List<DWDN_ClassDefinition>();

        public DWDN_Duchy(string id, string name, string description, string bannerCode, bool isEnabled = true)
        {
            Id = id;
            Name = name;
            Description = description;
            BannerCode = bannerCode;
            IsEnabled = isEnabled;
        }

        public DWDN_ClassDefinition GetClass(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return Classes.Count > 0 ? Classes[0] : null;
            return Classes.Find(c => c.Id.Equals(classId, StringComparison.OrdinalIgnoreCase)) 
                   ?? (Classes.Count > 0 ? Classes[0] : null);
        }
    }
}
