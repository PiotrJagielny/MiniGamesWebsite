﻿using Models;
using Models.Monopoly;
using Enums.Monopoly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Services.GamesServices.Monopoly.Board.Cells;
using Services.GamesServices.Monopoly.Update;
using Org.BouncyCastle.Asn1.Cmp;
using Models.MultiplayerConnection;
using Services.GamesServices.Monopoly.Board.ModalData;

namespace Services.GamesServices.Monopoly.Board
{
    public class MonopolyBoard
    {
        private List<MonopolyCell> Board;
        private Int MainPlayerTurnsOnIslandRemaining;
        private bool IsThisFirstLap;
        private MoneyObligation MoneyBondToPayOnUpdate;

        public MonopolyBoard()
        {
            MoneyBondToPayOnUpdate = new MoneyObligation();
            Board = new List<MonopolyCell>();
            MainPlayerTurnsOnIslandRemaining = new Int();
            Board = MonopolyBoardFactory.MakeBoard(ref MainPlayerTurnsOnIslandRemaining);
            IsThisFirstLap = true;
        } 

        public List<MonopolyCell> GetBoard()
        {
            List<MonopolyCell> BoardCopy = new List<MonopolyCell>();
            BoardCopy = Board;
            return BoardCopy;
        }

        public bool DontHaveMoneyToPay(MonopolyPlayer Debtor)
        {
            return CanAffordStaying(Debtor) == false && DoesCellHaveAnotherOwner(Debtor) == true;
        }

        private bool CanAffordStaying(MonopolyPlayer Visitor)
        {
            return Visitor.MoneyOwned >= Board[Visitor.OnCellIndex].GetBuyingBehavior().GetCosts().Stay;
        }

        public bool DoesCellHaveAnotherOwner(MonopolyPlayer PlayerStepped)
        {
            return IsNoOneCell(PlayerStepped.OnCellIndex) == false && IsPlayerCellOwner(PlayerStepped) == false;
        }

        public bool IsNoOneCell(int CellIndex)
        {
            return Board[CellIndex].GetBuyingBehavior().GetOwner() == PlayerKey.NoOne;
        }

        private bool IsPlayerCellOwner(MonopolyPlayer Player)
        {
            return Board[Player.OnCellIndex].GetBuyingBehavior().GetOwner() == Player.Key;
        }

        public void SetChampionship(string OnCellDisplay)
        {
            MonopolyCell? CellWithChampionship = Board.FirstOrDefault(
                c => c.GetBuyingBehavior().IsThereChampionship() == true
            );
            if (CellWithChampionship != null)
                CellWithChampionship.GetBuyingBehavior().GetChampionshipOff();


            MonopolyCell? CellToSetChampionship = Board.FirstOrDefault(
                c => c.OnDisplay() == OnCellDisplay
            );
            if(CellToSetChampionship != null)
                CellToSetChampionship.GetBuyingBehavior().SetChampionship();
        }

        public MonopolyModalParameters GetCellModalParameters(MonopolyPlayer MainPlayer)
        {
            DataToGetModalParameters Data = new DataToGetModalParameters();
            Data.Board = GetBoard();
            Data.MainPlayer = MainPlayer;
            Data.IsThisFirstLap = IsThisFirstLap;
            return Board[MainPlayer.OnCellIndex].GetModalParameters(Data);
        }

        public ModalResponseUpdate OnCellModalResponse(ModalResponseData Data)
        {
            int MainPlayerPos = Data.PlayersService.GetMainPlayer().OnCellIndex;
            ModalResponseUpdate UpdatedData = Board[MainPlayerPos].OnModalResponse(Data);
            return UpdatedData;
        }

        public MonopolyBoardUpdateData MakeBoardUpdateData()
        {
            MonopolyBoardUpdateData BoardUpdatedData = UpdateDataFactory.CreateBoardUpdateData();
            BoardUpdatedData.FormatBoardUpdateData(Board);
            return BoardUpdatedData;
        }

        public List<MonopolyCell> GetMainPlayerCells(PlayerKey MainPlayerKey)
        {
            return Board.FindAll(c => c.GetBuyingBehavior().GetOwner() == MainPlayerKey);
        }

        public MoneyObligation GetMoneyBond()
        {
            return MoneyBondToPayOnUpdate;
        }

        public void ResetBond()
        {
            MoneyBondToPayOnUpdate = new MoneyObligation();
        }

        public void MakeMoneyBond(in MonopolyPlayer MainPlayer)
        {
            try
            {
                MoneyBondToPayOnUpdate =  CalculateBond(MainPlayer);
            }
            catch
            {
                MoneyBondToPayOnUpdate = new MoneyObligation();
            }
        }

        private MoneyObligation CalculateBond(in MonopolyPlayer MainPlayer) 
        {
            MoneyObligation result = new MoneyObligation();
            if (DoesCellHaveAnotherOwner(MainPlayer))
            {
                result.PlayerGettingMoney = Board[MainPlayer.OnCellIndex].GetBuyingBehavior().GetOwner();
                result.PlayerLosingMoney = MainPlayer.Key;
                result.ObligationAmount = Board[MainPlayer.OnCellIndex].GetBuyingBehavior().GetCosts().Stay;
            }
            return result;
        }

        public void UpdateData(List<MonopolyCellUpdate> BoardUpdatedData)
        {
            for (int i = 0; i < BoardUpdatedData.Count; i++)
            {
                Board[i].UpdateData(BoardUpdatedData[i]);
            }
        }

        public bool IsAbleToMove()
        {
            if (MainPlayerTurnsOnIslandRemaining.Value > 1)
            {
                MainPlayerTurnsOnIslandRemaining.Value--;
                return false;
            }
            else
            {
                MainPlayerTurnsOnIslandRemaining.Value = 0;
            }

            return true;
        } 

        public int DistanceToCellFrom(int MainPlayerPos, string DestinationDisplay)
        {
            MonopolyCell Destination = Board.FirstOrDefault(c => c.OnDisplay() == DestinationDisplay);  
            int DestinationIndex = Board.IndexOf(Destination);

            if (DestinationIndex == -1)
                return 0;

            if(DestinationIndex < MainPlayerPos)
                return Math.Abs(DestinationIndex + Board.Count - MainPlayerPos);

            return Math.Abs(DestinationIndex - MainPlayerPos);
        }

        public int BuyCell(MonopolyPlayer MainPlayer, string WhatIsBought)
        {
            int BuyCost = Board[MainPlayer.OnCellIndex].CellBought(MainPlayer, WhatIsBought,ref Board);
            return BuyCost;
        }

        public int SellCell(string CellToSellDisplay)
        {
            MonopolyCell CellToSell = Board.FirstOrDefault(cell => cell.OnDisplay() == CellToSellDisplay);
            int BuyCost = CellToSell.GetBuyingBehavior().GetCosts().Buy;
            
            CellToSell.CellSold(ref Board);
            
            return BuyCost;
        }

        public void CheckIfSteppedOnIsland(MonopolyPlayer MainPlayer)
        {
            if (WillStayOnIsland(MainPlayer))
            {
                MainPlayerTurnsOnIslandRemaining.Value = 3;
            }
        }

        private bool WillStayOnIsland(MonopolyPlayer MainPlayer)
        {
            return Board[MainPlayer.OnCellIndex] is MonopolyIslandCell &&
                   MainPlayerTurnsOnIslandRemaining.Value == 0;
        }

        public bool DidCrossedStartCell(int MoveAmount, int MainPlayerPos)
        {
            bool Result = (MainPlayerPos + MoveAmount) >= Board.Count;

            if (Result == true)
                IsThisFirstLap = false;

            return Result;
        }

        public void EscapeFromIsland()
        {
            MainPlayerTurnsOnIslandRemaining.Value = 0;
        }
    }
}
