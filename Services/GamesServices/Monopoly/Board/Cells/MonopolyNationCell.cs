﻿using Enums.Monopoly;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Models.Monopoly;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Pkcs;
using Services.GamesServices.Monopoly.Board.Behaviours;
using Services.GamesServices.Monopoly.Board.Behaviours.Buying;
using Services.GamesServices.Monopoly.Board.Behaviours.Monopol;
using Services.GamesServices.Monopoly.Board.ModalData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Services.GamesServices.Monopoly.Board.Cells;

public class MonopolyNationCell : MonopolyCell
{
    private Nation OfNation;
    private City OfCity;

    private CellBuyingBehaviour BuyingBehaviour;
    private MonopolBehaviour monopolBehaviour;

    private Dictionary<string, Costs> BuildingCosts;

    private string CurrentBuilding;
   

    public MonopolyNationCell(Dictionary<string,Costs> BuildingToCostsMap, Nation nation, City city)
    {
        OfCity = city;
        OfNation = nation;
        BuildingCosts = BuildingToCostsMap;
        BuyingBehaviour = new CellAbleToBuyBehaviour(BuildingCosts[Consts.Monopoly.Field]);
        monopolBehaviour = new MonopolNationCellBehaviour();
        CurrentBuilding = "";
    }

    public string OnDisplay()
    {
        string result = "";
        result += $" Owner: {BuyingBehaviour.GetOwner().ToString()} |";
        result += $" Nation: {OfNation.ToString()} |";
        result += $" City: {OfCity.ToString()} |";

        if(string.IsNullOrEmpty(CurrentBuilding) == false && CurrentBuilding != Consts.Monopoly.NoBuildingBought)
            result += $" Stay Cost: {BuyingBehaviour.GetCosts().Stay}| ";

        result += $" Building: {CurrentBuilding} ";
        if (BuyingBehaviour.IsThereChampionship() == true)
            result += Consts.Monopoly.ChampionshipInfo;
        return result;
    }

    public MonopolyModalParameters GetModalParameters(DataToGetModalParameters Data)
    {
        if (Data.Board[Data.MainPlayer.OnCellIndex].GetBuyingBehavior().GetOwner() == PlayerKey.NoOne)
            return GetModalBuyingCell(Data);
        else if (Data.Board[Data.MainPlayer.OnCellIndex].GetBuyingBehavior().GetOwner() == Data.MainPlayer.Key)
            return GetModalEnhancingCell(Data);
        else
            return GetModalRepurchasingCell(Data);
    }

    private MonopolyModalParameters GetModalBuyingCell(DataToGetModalParameters Data)
    {
        StringModalParameters Parameters = new StringModalParameters();

        Parameters.Title = "What Do You wanna build?";
        Parameters.ButtonsContent.Add(Consts.Monopoly.NoBuildingBought);

        List<string> PossibleBuildingsToBuy = new List<string>();
        PossibleBuildingsToBuy.Add(Consts.Monopoly.Field);
        PossibleBuildingsToBuy.Add(Consts.Monopoly.OneHouse);
        PossibleBuildingsToBuy.Add(Consts.Monopoly.TwoHouses);
        PossibleBuildingsToBuy.Add(Consts.Monopoly.ThreeHouses);

        foreach (var building in PossibleBuildingsToBuy)
        {
            if (IsAbleToBuy(building, Data))
            {
                string ButtonToAdd = building;
                Parameters.Title += $" |{building} Buy: {BuildingCosts[building].Buy} Stay: {BuildingCosts[building].Stay}| ";
                
                Parameters.ButtonsContent.Add(ButtonToAdd);
            }
            
        }
        
        return new MonopolyModalParameters(Parameters, ModalShow.AfterMove);
    }

    private bool IsAbleToBuy(string Building, DataToGetModalParameters Data)
    {
        bool Result = Data.MainPlayer.MoneyOwned >= BuildingCosts[Building].Buy;

        if (Building == Consts.Monopoly.ThreeHouses && Data.IsThisFirstLap == true)
            return false;

        return Result;
    }

    private MonopolyModalParameters GetModalEnhancingCell(DataToGetModalParameters Data)
    {
        StringModalParameters Parameters = new StringModalParameters();

        Parameters.Title = "What Do You wanna build?";
        Parameters.ButtonsContent.Add(Consts.Monopoly.NoBuildingBought);

        List<string> PossibleEnhanceBuildings = MonopolyModalFactory.GetPossibleNationCellEnhancments();

        foreach (var building in PossibleEnhanceBuildings)
        {
            if (IsAbleToEnhance(Data, building))
                Parameters.ButtonsContent.Add(building);
        }

        return new MonopolyModalParameters(Parameters, ModalShow.AfterMove);
    }

    private bool IsAbleToEnhance(DataToGetModalParameters Data, string Building)
    {
        bool Result = Data.MainPlayer.MoneyOwned >= BuildingCosts[Building].Buy &&
                BuyingTiers.GetBuyTierNumber(Building) > BuyingTiers.GetBuyTierNumber(CurrentBuilding);

        if (Building == Consts.Monopoly.Hotel)
            Result = Result && CurrentBuilding == Consts.Monopoly.ThreeHouses;

        return Result;
    }

    private MonopolyModalParameters GetModalRepurchasingCell(DataToGetModalParameters Data)
    {
        if (MonopolyModalFactory.DoHaveToSellCell(Data.MainPlayer, BuyingBehaviour.GetCosts().Stay, BuyingBehaviour.GetOwner()))
            return MonopolyModalFactory.ChooseCellToSell(Data, BuyingBehaviour.GetCosts().Stay);

        if (IsAbleToRepurchaseCell(Data.MainPlayer.MoneyOwned))
            return MonopolyModalFactory.NoModalParameters();

        return RepurchaseCellModal();
    }

    private bool IsAbleToRepurchaseCell(int MainPlayerMoney)
    {
        int StayCost = BuyingBehaviour.GetCosts().Stay;
        int RepurchaseCost = (int)(BuyingBehaviour.GetCosts().Stay * Consts.Monopoly.CellRepurchaseMultiplayer);
        return MainPlayerMoney < StayCost + RepurchaseCost;
    }

    private static MonopolyModalParameters RepurchaseCellModal()
    {
        StringModalParameters Parameters = new StringModalParameters();

        Parameters.Title = "Do you want to repurchace this cell";
        Parameters.ButtonsContent.Add("Yes");
        Parameters.ButtonsContent.Add("No");

        return new MonopolyModalParameters(Parameters, ModalShow.AfterMove);
    }

    public ModalResponseUpdate OnModalResponse(ModalResponseData Data)
    {
        return MonopolyModalFactory.OnModalBuyableCellResponse(Data);
    }

    public int CellBought(MonopolyPlayer MainPlayer, string ModalResponse, ref List<MonopolyCell> CheckMonopol)
    {
        if (CanRepurchase(ModalResponse))
        {
            return RepurchaseCell(MainPlayer);
        }
        else if(CanBuy(ModalResponse))
        {
            return BuyCell(MainPlayer, ModalResponse, ref CheckMonopol);
        }

        return 0;
    }

    private int RepurchaseCell(MonopolyPlayer MainPlayer)
    {
        BuyingBehaviour.SetOwner(MainPlayer.Key);
        return (int)(BuyingBehaviour.GetCosts().Buy * Consts.Monopoly.CellRepurchaseMultiplayer);
    }

    private int BuyCell(MonopolyPlayer MainPlayer, string ModalResponse, ref List<MonopolyCell> CheckMonopol)
    {
        BuyingBehaviour.SetBaseCosts(BuildingCosts[ModalResponse]);
        BuyingBehaviour.SetOwner(MainPlayer.Key);
        CheckMonopol = monopolBehaviour.UpdateBoardMonopol(CheckMonopol, MainPlayer.OnCellIndex);
        CurrentBuilding = ModalResponse;
        return BuyingBehaviour.GetCosts().Buy;
    }

    private bool CanRepurchase(string ModalResponse)
    {
        return ModalResponse.ToLower() == "yes";
    }

    private bool CanBuy(string ModalResponse)
    {
        return ModalResponse.ToLower() != "no" &&
               ModalResponse.ToLower() != Consts.Monopoly.NoBuildingBought &&
               string.IsNullOrEmpty(ModalResponse) == false;
    }


    public void CellSold(ref List<MonopolyCell> MonopolChanges)
    {
        int CellIndex = MonopolChanges.IndexOf(this);
        BuyingBehaviour.SetOwner(PlayerKey.NoOne);
        MonopolChanges = monopolBehaviour.GetMonopolOff(MonopolChanges, CellIndex);
    }

    public string GetName()
    {
        return OfNation.ToString();
    }

    public void UpdateData(MonopolyCellUpdate UpdatedData)
    {
        BuyingBehaviour.SetOwner(UpdatedData.Owner);
        BuyingBehaviour.UpdateCosts(UpdatedData.NewCosts);
        CurrentBuilding = UpdatedData.NewBuilding;
    }

    public CellBuyingBehaviour GetBuyingBehavior()
    {
        return BuyingBehaviour;
    }
}
