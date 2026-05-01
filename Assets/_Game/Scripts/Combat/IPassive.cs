public interface IPassive
{
    bool CanTrigger(TriggerContext ctx);
    void OnTrigger(TriggerContext ctx);
}
