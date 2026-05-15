namespace SigmabotSync.ConfigTool.ViewModels
{
    /// <summary>Opción de día para <c>TrabajosProgramacion.DiaSemana</c> (0=domingo … 6=sábado, igual que <see cref="System.DayOfWeek"/>).</summary>
    public sealed class DiaSemanaOpcion
    {
        public int Valor { get; init; }
        public string Nombre { get; init; }
    }
}
