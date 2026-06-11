using FinancesApp_Module_User.Domain;

namespace FinancesApp_Api.Contracts.Requests.UserRequests;

public record UpdateUserRequest(Guid Id,
                                string Name, 
                                string ProfileImage)

{
   
    internal User MapToUser()
    {
        return new User(Id, Name, "", null, ProfileImage);
    }
}