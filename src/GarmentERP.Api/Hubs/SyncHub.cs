using Microsoft.AspNetCore.SignalR;

namespace GarmentERP.Api.Hubs;

public class SyncHub : Hub
{
    public async Task BroadcastSyncQueueChanged(int pending, int syncing, int done, int failed)
    {
        await Clients.All.SendAsync("OnSyncStatusUpdated", new { pending, syncing, done, failed });
    }

    public async Task BroadcastInventoryUpdated(Guid variantId, decimal newQuantity)
    {
        await Clients.All.SendAsync("OnInventoryUpdated", new { variantId, newQuantity });
    }
}
