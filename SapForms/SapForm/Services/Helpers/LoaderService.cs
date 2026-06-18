namespace SapForm.Services.Helpers
{
    public class LoaderService
    {
        public event Action? OnChange;
        private bool _isLoading;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged()
        {
            try
            {
                OnChange?.Invoke();
            }
            catch
            {
                // Do nothing
            }
        }
    }
}
