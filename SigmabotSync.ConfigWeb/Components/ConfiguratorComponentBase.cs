using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using SigmabotSync.ConfigWeb.Services;

namespace SigmabotSync.ConfigWeb.Components
{
    public abstract class ConfiguratorComponentBase : ComponentBase, IDisposable
    {
        [Inject]
        protected ConfiguratorState State { get; set; }

        private PropertyChangedEventHandler _propertyChangedHandler;

        protected override void OnInitialized()
        {
            _propertyChangedHandler = (_, __) => InvokeAsync(StateHasChanged);
            State.PropertyChanged += _propertyChangedHandler;
        }

        public void Dispose()
        {
            if (_propertyChangedHandler != null)
                State.PropertyChanged -= _propertyChangedHandler;
        }
    }
}
