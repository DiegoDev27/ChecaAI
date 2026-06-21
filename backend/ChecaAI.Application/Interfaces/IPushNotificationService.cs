using ChecaAI.Domain.Entities;

namespace ChecaAI.Application.Interfaces;

public interface IPushNotificationService
{
    Task<bool> SendAlertAsync(VotingAlert alert, VotingSession session);
}
