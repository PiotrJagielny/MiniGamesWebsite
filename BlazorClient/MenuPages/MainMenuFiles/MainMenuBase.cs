﻿using Microsoft.AspNetCore.Components;

namespace BlazorClient.MenuPages.MainMenuFiles
{
    public class MainMenuBase : ComponentBase
    {
        [Parameter]
        public string loggedUserName { get; set; }

        [Inject]
        public NavigationManager NavManager{ get; set; }

        protected void NavigateToSingleplayerGamesMenu()
        {
            NavManager.NavigateTo($"/SinglplayerGamesMenu/{loggedUserName}");
        }

        protected void NavigateToMultiplayerGamesMenu()
        {
            NavManager.NavigateTo($"/MultiplayerGamesMenu/{loggedUserName}");
        }
    }
}
