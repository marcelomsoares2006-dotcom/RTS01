using System;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

// Reflect over Assembly-CSharp because the EditMode test asmdef cannot reference it.
public class AdvancedEraStateVisualTests
{
    [Test]
    public void ResetNotifiesVisualListenersAfterEraChange()
    {
        Type state = Type.GetType("AdvancedEraState, Assembly-CSharp");
        Type eraType = Type.GetType("EraImperial.Units.Advanced.ImperialEra, EraImperial.Units.Advanced");
        Assert.That(state, Is.Not.Null);
        Assert.That(eraType, Is.Not.Null);
        MethodInfo set = state.GetMethod("Set", BindingFlags.Public | BindingFlags.Static);
        MethodInfo reset = state.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static);
        EventInfo changed = state.GetEvent("Changed", BindingFlags.Public | BindingFlags.Static);
        int notifications = 0;
        var observer = new EraObserver(() => notifications++);
        var eraParameter = Expression.Parameter(eraType, "era");
        Delegate listener = Expression.Lambda(changed.EventHandlerType,
            Expression.Call(Expression.Constant(observer), typeof(EraObserver).GetMethod("OnChanged")),
            eraParameter).Compile();

        reset.Invoke(null, null);
        changed.AddEventHandler(null, listener);
        try
        {
            set.Invoke(null, new[] { Enum.ToObject(eraType, 3) });
            reset.Invoke(null, null);
            Assert.That((int)state.GetProperty("Current", BindingFlags.Public | BindingFlags.Static).GetValue(null), Is.Zero);
            Assert.That(notifications, Is.EqualTo(2));
        }
        finally
        {
            changed.RemoveEventHandler(null, listener);
            reset.Invoke(null, null);
        }
    }

    public sealed class EraObserver
    {
        private readonly Action _onChanged;
        public EraObserver(Action onChanged) { _onChanged = onChanged; }
        public void OnChanged() { _onChanged(); }
    }
}
