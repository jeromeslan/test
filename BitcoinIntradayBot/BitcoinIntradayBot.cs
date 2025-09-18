using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BitcoinIntradayBot : Robot
    {
        // ======= Paramètres de Gestion des Risques =======
        [Parameter("Risk % per Trade", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Group = "Risk Management")]
        public double RiskPercent { get; set; }

        [Parameter("Max Daily Loss %", DefaultValue = 5.0, MinValue = 1.0, MaxValue = 20.0, Group = "Risk Management")]
        public double MaxDailyLossPercent { get; set; }

        [Parameter("Max Positions per Side", DefaultValue = 3, MinValue = 1, MaxValue = 5, Group = "Risk Management")]
        public int MaxPositionsPerSide { get; set; }

        // ======= Paramètres de Session =======
        [Parameter("Start Hour UTC", DefaultValue = 8, MinValue = 0, MaxValue = 23, Group = "Trading Session")]
        public int StartHourUtc { get; set; }

        [Parameter("End Hour UTC", DefaultValue = 22, MinValue = 0, MaxValue = 23, Group = "Trading Session")]
        public int EndHourUtc { get; set; }

        [Parameter("Trade Mondays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeMondays { get; set; }

        [Parameter("Trade Tuesdays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeTuesdays { get; set; }

        [Parameter("Trade Wednesdays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeWednesdays { get; set; }

        [Parameter("Trade Thursdays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeThursdays { get; set; }

        [Parameter("Trade Fridays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeFridays { get; set; }

        [Parameter("Trade Saturdays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeSaturdays { get; set; }

        [Parameter("Trade Sundays", DefaultValue = true, Group = "Trading Days")]
        public bool TradeSundays { get; set; }

        // ======= Paramètres de Direction =======
        [Parameter("Enable Long Trades", DefaultValue = true, Group = "Trade Direction")]
        public bool EnableLongTrades { get; set; }

        [Parameter("Enable Short Trades", DefaultValue = true, Group = "Trade Direction")]
        public bool EnableShortTrades { get; set; }

        // ======= Paramètres des Indicateurs =======
        [Parameter("RSI Period", DefaultValue = 14, MinValue = 2, MaxValue = 100, Group = "Technical Indicators")]
        public int RSIPeriod { get; set; }

        [Parameter("RSI Oversold Level", DefaultValue = 30, MinValue = 10, MaxValue = 40, Group = "Technical Indicators")]
        public double RSIOversold { get; set; }

        [Parameter("RSI Overbought Level", DefaultValue = 70, MinValue = 60, MaxValue = 90, Group = "Technical Indicators")]
        public double RSIOverbought { get; set; }

        [Parameter("ADX Period", DefaultValue = 14, MinValue = 2, MaxValue = 100, Group = "Technical Indicators")]
        public int ADXPeriod { get; set; }

        [Parameter("ADX Threshold", DefaultValue = 25, MinValue = 10, MaxValue = 50, Group = "Technical Indicators")]
        public double ADXThreshold { get; set; }

        [Parameter("EMA Fast Period", DefaultValue = 21, MinValue = 2, MaxValue = 200, Group = "Technical Indicators")]
        public int EmaFastPeriod { get; set; }

        [Parameter("EMA Slow Period", DefaultValue = 50, MinValue = 5, MaxValue = 400, Group = "Technical Indicators")]
        public int EmaSlowPeriod { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2, MaxValue = 200, Group = "Technical Indicators")]
        public int ATRPeriod { get; set; }

        // ======= Paramètres de Stop Loss et Take Profit =======
        [Parameter("ATR Stop Loss Multiplier", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0, Group = "Stop Loss")]
        public double ATRStopLossMultiplier { get; set; }

        [Parameter("ATR Trailing Stop Multiplier", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 10.0, Group = "Stop Loss")]
        public double ATRTrailingStopMultiplier { get; set; }

        [Parameter("Enable Trailing Stop", DefaultValue = true, Group = "Stop Loss")]
        public bool EnableTrailingStop { get; set; }

        [Parameter("TP1 Risk Multiple", DefaultValue = 1.0, MinValue = 0.2, MaxValue = 5.0, Group = "Take Profit")]
        public double TP1RiskMultiple { get; set; }

        [Parameter("TP2 Risk Multiple", DefaultValue = 2.0, MinValue = 0.2, MaxValue = 10.0, Group = "Take Profit")]
        public double TP2RiskMultiple { get; set; }

        [Parameter("TP3 Risk Multiple", DefaultValue = 3.0, MinValue = 0.2, MaxValue = 15.0, Group = "Take Profit")]
        public double TP3RiskMultiple { get; set; }

        // ======= Paramètres de Répartition des Volumes =======
        [Parameter("TP1 Volume Fraction", DefaultValue = 0.33, MinValue = 0.05, MaxValue = 0.9, Group = "Volume Distribution")]
        public double TP1VolumeFraction { get; set; }

        [Parameter("TP2 Volume Fraction", DefaultValue = 0.33, MinValue = 0.05, MaxValue = 0.9, Group = "Volume Distribution")]
        public double TP2VolumeFraction { get; set; }

        [Parameter("TP3 Volume Fraction", DefaultValue = 0.34, MinValue = 0.05, MaxValue = 0.9, Group = "Volume Distribution")]
        public double TP3VolumeFraction { get; set; }

        // ======= Paramètres de Filtrage =======
        [Parameter("Min Spread (Pips)", DefaultValue = 0, MinValue = 0, MaxValue = 50, Group = "Market Filters")]
        public double MinSpreadPips { get; set; }

        [Parameter("Max Spread (Pips)", DefaultValue = 100, MinValue = 1, MaxValue = 1000, Group = "Market Filters")]
        public double MaxSpreadPips { get; set; }

        [Parameter("Min ATR (Pips)", DefaultValue = 10, MinValue = 1, MaxValue = 1000, Group = "Market Filters")]
        public double MinATRPips { get; set; }

        // ======= Variables Privées =======
        private RelativeStrengthIndex _rsi;
        private DirectionalMovementSystem _dms;
        private ExponentialMovingAverage _emaFast, _emaSlow;
        private AverageTrueRange _atr;
        private string _labelBase;
        private double _dailyStartBalance;
        private DateTime _lastDailyReset;

        // ======= Statistiques =======
        private int _totalTrades;
        private int _winningTrades;
        private double _totalProfit;
        private double _totalLoss;

        protected override void OnStart()
        {
            // Initialisation des indicateurs
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, RSIPeriod);
            _dms = Indicators.DirectionalMovementSystem(ADXPeriod);
            _emaFast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaFastPeriod);
            _emaSlow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaSlowPeriod);
            _atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.Exponential);

            // Initialisation des variables
            _labelBase = $"{Name}-{SymbolName}-{TimeFrame}";
            _dailyStartBalance = Account.Balance;
            _lastDailyReset = Server.Time.Date;

            // Validation des paramètres
            ValidateParameters();

            Print($"BitcoinIntradayBot démarré sur {SymbolName} {TimeFrame}");
            Print($"Session de trading: {StartHourUtc}:00 - {EndHourUtc}:00 UTC");
            Print($"Risque par trade: {RiskPercent}%");
            Print($"Perte journalière maximale: {MaxDailyLossPercent}%");
        }

        protected override void OnBar()
        {
            // Vérifications préliminaires
            if (!IsTradingAllowed())
                return;

            // Reset quotidien des statistiques
            CheckDailyReset();

            // Analyse technique
            var signal = AnalyzeMarket();
            
            if (signal == TradeSignal.None)
                return;

            // Comptage des positions existantes
            int longPositions = CountPositions(TradeType.Buy);
            int shortPositions = CountPositions(TradeType.Sell);

            // Exécution des trades
            if (signal == TradeSignal.Long && EnableLongTrades && longPositions < MaxPositionsPerSide)
            {
                ExecuteTradeSequence(TradeType.Buy);
            }
            else if (signal == TradeSignal.Short && EnableShortTrades && shortPositions < MaxPositionsPerSide)
            {
                ExecuteTradeSequence(TradeType.Sell);
            }
        }

        protected override void OnTick()
        {
            if (EnableTrailingStop)
            {
                UpdateTrailingStops();
            }
        }

        protected override void OnPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (!position.Label.StartsWith(_labelBase))
                return;

            // Mise à jour des statistiques
            _totalTrades++;
            if (position.NetProfit > 0)
            {
                _winningTrades++;
                _totalProfit += position.NetProfit;
            }
            else
            {
                _totalLoss += Math.Abs(position.NetProfit);
            }

            // Log de la position fermée
            Print($"Position fermée: {position.TradeType} {position.VolumeInUnits} unités, " +
                  $"P&L: {position.NetProfit:F2} {Account.Asset.Name}");

            // Affichage des statistiques
            if (_totalTrades > 0)
            {
                double winRate = (_winningTrades / (double)_totalTrades) * 100;
                Print($"Statistiques: {_totalTrades} trades, {winRate:F1}% de réussite, " +
                      $"Profit total: {_totalProfit - _totalLoss:F2} {Account.Asset.Name}");
            }
        }

        private void ValidateParameters()
        {
            if (TP1VolumeFraction + TP2VolumeFraction + TP3VolumeFraction != 1.0)
            {
                Print("ATTENTION: La somme des fractions de volume TP n'égale pas 1.0");
            }

            if (EmaFastPeriod >= EmaSlowPeriod)
            {
                Print("ATTENTION: La période EMA rapide doit être inférieure à la période EMA lente");
            }

            if (RSIOversold >= RSIOverbought)
            {
                Print("ATTENTION: Le niveau RSI de survente doit être inférieur au niveau de surachat");
            }
        }

        private bool IsTradingAllowed()
        {
            // Vérification de la session horaire
            if (!IsTradingWindow())
                return false;

            // Vérification du jour de la semaine
            if (!IsTradingDay())
                return false;

            // Vérification de la limite de perte journalière
            if (HasHitDailyLossLimit())
                return false;

            // Vérification des conditions de marché
            if (!AreMarketConditionsGood())
                return false;

            return true;
        }

        private bool IsTradingWindow()
        {
            int currentHour = Server.Time.Hour;
            
            if (StartHourUtc <= EndHourUtc)
            {
                return currentHour >= StartHourUtc && currentHour < EndHourUtc;
            }
            else
            {
                // Session qui traverse minuit
                return currentHour >= StartHourUtc || currentHour < EndHourUtc;
            }
        }

        private bool IsTradingDay()
        {
            var dayOfWeek = Server.Time.DayOfWeek;
            
            return dayOfWeek switch
            {
                DayOfWeek.Monday => TradeMondays,
                DayOfWeek.Tuesday => TradeTuesdays,
                DayOfWeek.Wednesday => TradeWednesdays,
                DayOfWeek.Thursday => TradeThursdays,
                DayOfWeek.Friday => TradeFridays,
                DayOfWeek.Saturday => TradeSaturdays,
                DayOfWeek.Sunday => TradeSundays,
                _ => false
            };
        }

        private bool HasHitDailyLossLimit()
        {
            var today = Server.Time.Date;
            double dailyPnL = 0;

            // Calcul du P&L des positions fermées aujourd'hui
            var todayTrades = History.Where(h => h.EntryTime.Date == today && 
                                                 h.SymbolName == SymbolName);
            
            foreach (var trade in todayTrades)
            {
                dailyPnL += trade.NetProfit;
            }

            // Ajout du P&L des positions ouvertes
            var openPositions = Positions.Where(p => p.SymbolName == SymbolName && 
                                                     p.Label.StartsWith(_labelBase));
            
            foreach (var position in openPositions)
            {
                dailyPnL += position.NetProfit;
            }

            double lossLimit = -_dailyStartBalance * (MaxDailyLossPercent / 100.0);
            
            if (dailyPnL <= lossLimit)
            {
                Print($"Limite de perte journalière atteinte: {dailyPnL:F2} <= {lossLimit:F2}");
                return true;
            }

            return false;
        }

        private bool AreMarketConditionsGood()
        {
            // Vérification du spread
            double spreadPips = Symbol.Spread / Symbol.PipSize;
            if (spreadPips < MinSpreadPips || spreadPips > MaxSpreadPips)
            {
                return false;
            }

            // Vérification de la volatilité (ATR)
            double atrPips = _atr.Result.LastValue / Symbol.PipSize;
            if (atrPips < MinATRPips)
            {
                return false;
            }

            return true;
        }

        private TradeSignal AnalyzeMarket()
        {
            // Vérification que nous avons assez de données
            if (_rsi.Result.Count < 2 || _dms.ADX.Count < 2 || 
                _emaFast.Result.Count < 2 || _emaSlow.Result.Count < 2)
            {
                return TradeSignal.None;
            }

            // Valeurs actuelles des indicateurs
            double currentRSI = _rsi.Result.LastValue;
            double currentADX = _dms.ADX.LastValue;
            double currentEMAFast = _emaFast.Result.LastValue;
            double currentEMASlow = _emaSlow.Result.LastValue;
            double currentPrice = Bars.ClosePrices.LastValue;

            // Analyse de la tendance
            bool upTrend = currentPrice > currentEMAFast && currentEMAFast > currentEMASlow;
            bool downTrend = currentPrice < currentEMAFast && currentEMAFast < currentEMASlow;

            // Filtre de force de tendance
            bool strongTrend = currentADX > ADXThreshold;

            // Filtre RSI (éviter les extrêmes)
            bool rsiNeutralForLong = currentRSI > RSIOversold && currentRSI < 60;
            bool rsiNeutralForShort = currentRSI < RSIOverbought && currentRSI > 40;

            // Signaux d'entrée
            if (upTrend && strongTrend && rsiNeutralForLong)
            {
                return TradeSignal.Long;
            }
            else if (downTrend && strongTrend && rsiNeutralForShort)
            {
                return TradeSignal.Short;
            }

            return TradeSignal.None;
        }

        private void ExecuteTradeSequence(TradeType tradeType)
        {
            // Calcul du volume total basé sur le risque
            double totalVolume = CalculatePositionSize();
            if (totalVolume <= 0)
            {
                Print("Volume calculé invalide, trade annulé");
                return;
            }

            // Calcul des volumes pour chaque TP
            double volume1 = Symbol.NormalizeVolumeInUnits(totalVolume * TP1VolumeFraction, RoundingMode.Down);
            double volume2 = Symbol.NormalizeVolumeInUnits(totalVolume * TP2VolumeFraction, RoundingMode.Down);
            double volume3 = Symbol.NormalizeVolumeInUnits(totalVolume - volume1 - volume2, RoundingMode.Down);

            if (volume1 <= 0 || volume2 <= 0 || volume3 <= 0)
            {
                Print("Volumes TP invalides, trade annulé");
                return;
            }

            // Calcul du stop loss
            double atrValue = _atr.Result.LastValue;
            double stopLossDistance = atrValue * ATRStopLossMultiplier;
            
            double stopLossPrice;
            if (tradeType == TradeType.Buy)
            {
                stopLossPrice = Symbol.Ask - stopLossDistance;
            }
            else
            {
                stopLossPrice = Symbol.Bid + stopLossDistance;
            }

            // Calcul des take profits
            double stopLossPips = Math.Abs(Symbol.Ask - stopLossPrice) / Symbol.PipSize;
            double tp1Pips = stopLossPips * TP1RiskMultiple;
            double tp2Pips = stopLossPips * TP2RiskMultiple;
            double tp3Pips = stopLossPips * TP3RiskMultiple;

            // Exécution des ordres
            var result1 = ExecuteMarketOrder(tradeType, SymbolName, volume1, $"{_labelBase}-TP1", 
                                           stopLossPips, tp1Pips);
            var result2 = ExecuteMarketOrder(tradeType, SymbolName, volume2, $"{_labelBase}-TP2", 
                                           stopLossPips, tp2Pips);
            var result3 = ExecuteMarketOrder(tradeType, SymbolName, volume3, $"{_labelBase}-TP3", 
                                           stopLossPips, tp3Pips);

            if (result1.IsSuccessful && result2.IsSuccessful && result3.IsSuccessful)
            {
                Print($"Séquence de trades {tradeType} exécutée avec succès:");
                Print($"  TP1: {volume1} unités à TP {tp1Pips:F1} pips");
                Print($"  TP2: {volume2} unités à TP {tp2Pips:F1} pips");
                Print($"  TP3: {volume3} unités à TP {tp3Pips:F1} pips");
                Print($"  Stop Loss: {stopLossPips:F1} pips");
            }
            else
            {
                Print($"Erreur lors de l'exécution de la séquence {tradeType}");
            }
        }

        private double CalculatePositionSize()
        {
            double riskAmount = Account.Balance * (RiskPercent / 100.0);
            double atrValue = _atr.Result.LastValue;
            double stopLossDistance = atrValue * ATRStopLossMultiplier;
            double stopLossPips = stopLossDistance / Symbol.PipSize;
            
            if (stopLossPips <= 0)
                return 0;

            double pipValue = Symbol.PipValue;
            if (pipValue <= 0)
                return 0;

            double volumeInUnits = (riskAmount / stopLossPips) / (pipValue / Symbol.LotSize);
            return Symbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
        }

        private void UpdateTrailingStops()
        {
            var positions = Positions.Where(p => p.SymbolName == SymbolName && 
                                                p.Label.StartsWith(_labelBase));

            foreach (var position in positions)
            {
                double atrValue = _atr.Result.LastValue;
                double trailingDistance = atrValue * ATRTrailingStopMultiplier;

                if (position.TradeType == TradeType.Buy)
                {
                    double newStopLoss = Symbol.Bid - trailingDistance;
                    
                    if (!position.StopLoss.HasValue || newStopLoss > position.StopLoss.Value)
                    {
                        ModifyPosition(position, newStopLoss, position.TakeProfit);
                    }
                }
                else
                {
                    double newStopLoss = Symbol.Ask + trailingDistance;
                    
                    if (!position.StopLoss.HasValue || newStopLoss < position.StopLoss.Value)
                    {
                        ModifyPosition(position, newStopLoss, position.TakeProfit);
                    }
                }
            }
        }

        private int CountPositions(TradeType tradeType)
        {
            return Positions.Count(p => p.SymbolName == SymbolName && 
                                       p.Label.StartsWith(_labelBase) && 
                                       p.TradeType == tradeType);
        }

        private void CheckDailyReset()
        {
            var today = Server.Time.Date;
            
            if (today > _lastDailyReset)
            {
                _dailyStartBalance = Account.Balance;
                _lastDailyReset = today;
                
                Print($"Reset quotidien - Nouveau solde de référence: {_dailyStartBalance:F2} {Account.Asset.Name}");
            }
        }

        protected override void OnStop()
        {
            Print("BitcoinIntradayBot arrêté");
            
            if (_totalTrades > 0)
            {
                double winRate = (_winningTrades / (double)_totalTrades) * 100;
                double netProfit = _totalProfit - _totalLoss;
                
                Print("=== STATISTIQUES FINALES ===");
                Print($"Nombre total de trades: {_totalTrades}");
                Print($"Taux de réussite: {winRate:F1}%");
                Print($"Profit total: {_totalProfit:F2} {Account.Asset.Name}");
                Print($"Perte totale: {_totalLoss:F2} {Account.Asset.Name}");
                Print($"Profit net: {netProfit:F2} {Account.Asset.Name}");
            }
        }
    }

    public enum TradeSignal
    {
        None,
        Long,
        Short
    }
}