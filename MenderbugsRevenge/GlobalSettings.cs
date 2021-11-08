namespace MenderbugsRevenge
{
    public class GlobalSettings
    {
        public enum PunishmentType
        {
            Die,
            OneDamage,
            None
        }

        public PunishmentType ActionOnBreak = PunishmentType.Die;
    }
}
