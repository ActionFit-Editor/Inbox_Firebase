using System;
using System.Threading;
using System.Threading.Tasks;
using ActionFitConnectivity = ActionFit.Connectivity.IConnectivityService;

namespace ActionFit.Inbox.Firebase
{
    public sealed class ActionFitConnectivityAdapter : IConnectivityService
    {
        private readonly ActionFitConnectivity _connectivity;

        public ActionFitConnectivityAdapter(ActionFitConnectivity connectivity)
        {
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
        }

        public bool IsOnline => _connectivity.State == ActionFit.Connectivity.ConnectivityState.Online;

        public Task WaitForOnlineAsync(CancellationToken cancellationToken = default)
        {
            return _connectivity.WaitForOnlineAsync(cancellationToken);
        }
    }
}
