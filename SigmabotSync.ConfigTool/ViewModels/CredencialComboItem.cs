using System;

namespace SigmabotSync.ConfigTool.ViewModels
{
    /// <summary>Elemento de combo para elegir una fila de Credenciales por Id.</summary>
    public sealed class CredencialComboItem : IEquatable<CredencialComboItem>
    {
        public CredencialComboItem(int id, string display)
        {
            Id = id;
            Display = display ?? string.Empty;
        }

        public int Id { get; }

        public string Display { get; }

        public bool Equals(CredencialComboItem other) => other != null && Id == other.Id;

        public override bool Equals(object obj) => obj is CredencialComboItem o && Equals(o);

        public override int GetHashCode() => Id;

        public override string ToString() => Display;
    }
}
