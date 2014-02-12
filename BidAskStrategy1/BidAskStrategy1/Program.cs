using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using StClientLib;


namespace BidAskStrategy1
{

    class newTestClass
    {

        private Quote LastQuote;
        private StServerClass SmartServer;
        private List<Tiker> InfoTikers;     // Список всех инструментов
        private List<string> InfoTypes;
        private List<Bar> InfoBars;
        private List<string> InfoPortfolios;
        private List<double> CollectedCloseList_SBRF;
        private List<double> CollectedCloseList_SBER;
        private StreamWriter tw;
        private StreamWriter twclose;
        private StreamWriter twopen;

        private Stopwatch sw1;

        int closeFlag = 0;
        int openFlag = 0;
        int closeReady = 1;
        int openReady = 1;


        double o0_value = 0;
        double o1_value = 0;
        double c1_value = 0;

        int currBar = 1;

        int buyDone = 0;
        int sellDone = 0;

        int indicate = 0;
        int userMachineFaster = 1;  //assume that local time runs faster than server time, because if the first tick arrives too late
                                    //we will not know the timing difference and in that case it is better to
                                    //make such an assumption since it will protect us in any case, although at some cost 
                                    //(if local time runs faster or equal, we lose 5min - [actual difference] time)
                                    //(if local time runs slower, we lose 5 min + [actual difference] time)
        int minDifference = 0;
        int secDifference = 0;

        int deltaDef = 0;
        int deltaDef2 = 0;
        int deltaClose = 0;

        int startedFlag = 0;
        int barsToGet = 0;

        double BuySpread_SBRF_SBER = 0;
        double SellSpread_SBRF_SBER = 0;

        double BuySpread_613_913 = 0;
        double SellSpread_613_913 = 0;

        double currBestBid_RTS613 = -1;
        double currBestAsk_RTS613 = -1;
        double currBestBid_RTS913 = -1;
        double currBestAsk_RTS913 = -1;
        double currBestBid_SBER   = -1;
        double currBestAsk_SBER   = -1;
        double currBestBid_SBRF   = -1;
        double currBestAsk_SBRF   = -1;

        int BuyUpdatedRTS = 0;
        int SellUpdatedRTS = 0;
        int BuyUpdatedSB = 0;
        int SellUpdatedSB = 0;

        int collectedNum_SBRF = 0;
        int collectedNum_SBER = 0;       

        static int cookieInit = 432198765;
     
        private void doActions()
        {


            try
            {

                SmartServer = new StServerClass();                                                             /* create SmartCom object*/
                InfoTypes = new List<string>();
                InfoTikers = new List<Tiker>();
                InfoBars = new List<Bar>();
                InfoPortfolios = new List<string>();
                CollectedCloseList_SBRF = new List<double>();
                CollectedCloseList_SBER = new List<double>();


                sw1 = new Stopwatch();

                SmartServer.AddSymbol += new _IStClient_AddSymbolEventHandler(SmartServer_AddSymbol);          /* add listener for AddSymbol events   */
                SmartServer.UpdateQuote += new _IStClient_UpdateQuoteEventHandler(SmartServer_UpdateQuote);    /* add listener for UpdateQuote events */
                SmartServer.AddTick += new _IStClient_AddTickEventHandler(SmartServer_AddTick);                /* add listener for AddTick events     */
                SmartServer.UpdateBidAsk += new _IStClient_UpdateBidAskEventHandler(SmartServer_UpdateBidAsk); /* add listener for UpdateBidAsk events*/
                SmartServer.AddBar += new _IStClient_AddBarEventHandler(SmartServer_AddBar);                   /* add listener for AddBar events      */
                SmartServer.AddPortfolio += new _IStClient_AddPortfolioEventHandler(SmartServer_AddPortfolio); /* add listener for AddPortfolio events*/
                SmartServer.SetPortfolio += new _IStClient_SetPortfolioEventHandler(SmartServer_SetPortfolio); /* add listener for SetPortfolio events*/
                SmartServer.OrderSucceeded += new _IStClient_OrderSucceededEventHandler(SmartServer_OrderSucceeded); /* add listener for OrderSucceeded events */
                SmartServer.OrderFailed += new _IStClient_OrderFailedEventHandler(SmartServer_OrderFailed);          /* add listener for OrderFailed events    */



                File.Delete("RTS-6.13_FT.txt");
                File.Delete("BidAskSpread_Updates.txt");
                //       File.Delete("RTSo-6.13_FT.txt");
                //       File.Delete("RTSc-6.13_FT.txt");


                try
                {

                    do
                    {

                        SmartServer.connect("82.204.220.34", 8090, "12XXXX", "SynterXXXX");   /* connect to server */
                    }
                    while (!SmartServer.IsConnected());

                    if (SmartServer.IsConnected())     //proceed
                    {                                                            


                        try
                        {

                            SmartServer.GetPrortfolioList();                                     /*get list of portfolios available for current login */

                            //     SmartServer.ListenPortfolio("BP12829-RF-02");                 /*receive notifications about portfolio changes      */

                            SmartServer.GetSymbols();                                            /* get symbols list                                  */


                            //     SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);  

                            ListenSymbol("RTS-6.13_FT");                                         /* subscribe for ticks, bids & quotes of the         */

                            ListenSymbol("RTS-9.13_FT");

                            ListenSymbol("SBRF-6.13_FT");

                            ListenSymbol("SBER");


                                                                                                 /* specified symbol                                  */
                            //     new Thread(DefinePreciseClose).Start();
                            //     new Thread(DefinePreciseOpen) .Start();


                            //     this thread is going to detect xx.59.59 and revert order if necessary

                            //new Thread(InvertPlaceOrder).Start();

                            //     current thread is going to detect time xx.00.00                                                          



                            Console.WriteLine("Waiting for activation period to start [10.00am - 18.45pm]");

                            while (true)
                            {
                                if (userMachineFaster == 1) // fetch bars 5 minuntes after xx.00.00
                                    deltaDef = 11;
                                else                        // fetch bars right at xx.00.00
                                    deltaDef = 12;

                                //assume that local machine runs faster than server

                                if (startedFlag == 0 && 
                                   ((DateTime.Now.Hour <= 18 && DateTime.Now.Hour >= 11) || (DateTime.Now.Hour == 19 && DateTime.Now.Minute < 45)) && 
                                    DateTime.Now.Second >= 20)

                                { 
                                    Console.WriteLine("launching strategy...");

                                    //start close collecting thread here which should collect corresponding number of closes
                                    //and then move control to bid/ask handling thread
                                

                                    if(DateTime.Now.Hour == 11 && DateTime.Now.Minute <= 45)   //launch within interval [10.00am - 10.45am] inclusively

                                    {

                                    if (DateTime.Now.Minute > 0)                               //need to get [DateTime.Now.Minute] number of historical minute bars right now    

                                      {
                                          SmartServer.GetBars("SBRF-6.13_FT", StBarInterval.StBarInterval_1Min, DateTime.Now, DateTime.Now.Minute);

                                          SmartServer.GetBars("SBER", StBarInterval.StBarInterval_1Min, DateTime.Now, DateTime.Now.Minute);                                    
                                      }                                                                                               
                                         
                                    barsToGet = 45 - DateTime.Now.Minute;                      //remaining bars to get

                                    while (collectedNum_SBRF != DateTime.Now.Minute ||
                                           collectedNum_SBER != DateTime.Now.Minute)
                                    { }

                                    Console.WriteLine("\nClose collection done. Now adding remaining closes");

                                    }//end if 


                               else                                                            //launch after 10.45am but before 18.45pm
                                                     
                                    {
                                        Console.WriteLine("need to fetch " + ((DateTime.Now.Hour - 11) * 60 + DateTime.Now.Minute) + " historical minute bars");

                                        SmartServer.GetBars("SBRF-6.13_FT", StBarInterval.StBarInterval_1Min, DateTime.Now, (DateTime.Now.Hour - 11)*60 + DateTime.Now.Minute);

                                        SmartServer.GetBars("SBER", StBarInterval.StBarInterval_1Min, DateTime.Now, (DateTime.Now.Hour - 11)*60 + DateTime.Now.Minute);

                                        barsToGet = 0;

                                        while (collectedNum_SBRF != ((DateTime.Now.Hour - 11) * 60 + DateTime.Now.Minute) ||
                                               collectedNum_SBER != ((DateTime.Now.Hour - 11) * 60 + DateTime.Now.Minute))
                                        { }

                                        Console.WriteLine("\nClose collection done.");

                                    }//end else


                                   new Thread(CollectCloseValues).Start();

                                   startedFlag = 1;

                                   new Thread(TraceMorningTime).Start(); 

                                }//end if                             

                                

                            }//end while                      


                            //Console.ReadKey();
                            

                            //SmartServer.disconnect();              /* disconnect after we receive everything we need */ 

                        } /* end try getsymbols*/

                        catch (Exception Error)
                        {

                            Console.WriteLine("Error occured when getting symbols list from server");

                        } /* end catch getsymbols */

                    }   /* end if connected */


                } /* end try connect */

                catch (Exception Error)
                {

                    Console.WriteLine("Error occured when connecting to SmartCom server");

                } /* end catch connect */


            }  /* end try create */


            catch (Exception Error)
            {

                Console.WriteLine("Error occured when creating SmartCom object");

            }  /* end catch create */



        } /* end main */


        ///////////////////////////////////////////////////////////////listeners///////////////

        private void SmartServer_AddSymbol(int row, int nrows, string symbol, string short_name, string long_name, string type, int decimals, int lot_size, double punkt, double step, string sec_ext_id, string sec_exch_name, System.DateTime expiry_date, double days_before_expiry, double strike)
        {
            // добавить инструмент в список            
            InfoTikers.Add(new Tiker(symbol, short_name, long_name, step, punkt, decimals, sec_ext_id, sec_exch_name, expiry_date, days_before_expiry, strike));

            if ((symbol == "RTS-6.13_FT") || (symbol == "RTS-9.13_FT") || (symbol == "SBRF-6.13_FT") || (symbol == "SBER"))//|| symbol == "RTSo-6.13_FT" || symbol == "RTSc-6.13_FT")
            {
                tw = File.AppendText(symbol + ".txt");

                Console.WriteLine("\n[SmartServer_AddSymbol message]: symbol detected");
                tw.WriteLine("\n[SmartServer_AddSymbol message]: symbol detected");
                Console.WriteLine(InfoTikers[InfoTikers.Count - 1].ToString());   /* which implies the last added element */
                tw.WriteLine(InfoTikers[InfoTikers.Count - 1].ToString());
                tw.Close();

            }


            if (InfoTypes.IndexOf(type) == -1)
                InfoTypes.Add(type);

        }

        private void SmartServer_UpdateQuote(string symbol, System.DateTime datetime, double open, double high, double low, double close, double last, double volume, double size, double bid, double ask, double bidsize, double asksize, double open_int, double go_buy, double go_sell, double go_base, double go_base_backed, double high_limit, double low_limit, int trading_status, double volat, double theor_price)
        {
            if (LastQuote == null || (LastQuote != null && LastQuote.Code != symbol))  /* updated symbol has never been saved so far */
            {
                if (symbol == "RTS-6.13_FT" || symbol == "RTS-9.13_FT" || symbol == "SBRF-6.13_FT" || symbol == "SBER")// || symbol == "RTSc-6.13_FT")
                {
                    LastQuote = new Quote(symbol, datetime, last, volume, trading_status, UpDateQuote); // создать котировку для инструмента
                    /*      
                               tw = File.AppendText(symbol + ".txt");
                                         
                          Console.WriteLine("\n[SmartServer_UpdateQuote message]: quote created for " + symbol);
                               tw.WriteLine("\n[SmartServer_UpdateQuote message]: quote created for " + symbol);
                          Console.WriteLine("[Symbol = " + symbol + "]\n[DateTime = " + datetime.ToShortDateString() + "]\n[Last = " + last + "]\n[Volume = " + volume + "]\n[TradingStatus = " + trading_status + "]");
                               tw.WriteLine("[Symbol = " + symbol + "]\n[DateTime = " + datetime.ToShortDateString() + "]\n[Last = " + last + "]\n[Volume = " + volume + "]\n[TradingStatus = " + trading_status + "]");
                               tw.Close();
                    */
                }
            }
            else
                LastQuote.UpDate(trading_status); // the last saved quote has been updated now
        }                                         // GUI labels corresponding to instrument's quote are updated by separate thread     


        private void SmartServer_AddTick(string symbol, System.DateTime datetime, double price, double volume, string tradeno, StClientLib.StOrder_Action action)
        {
            if (LastQuote != null) // обновить котировку. update is shown ONLY if 'symbol' corresponds to last quote symbol
            {
                DateTime now1 = DateTime.Now;

                long LastNo = 0;
                long.TryParse(tradeno, out LastNo);

                if (LastQuote.Code == symbol)
                {
                    LastQuote.UpDate(datetime, price, volume, LastNo, action);


                    if (indicate == 0)
                    {


                        if (now1.Minute > datetime.Minute)               //user machine clock runs faster than server clock.
                                                                         //in this case it is critically important to adjust bar fetch times
                                                                         //i.e. fetch bars a bit later than .00.00
                        {
                            userMachineFaster = 1;

                            Console.Write("\nLocal time exceeds Server time by ");

                            if (now1.Second >= datetime.Second)
                            {
                                secDifference = now1.Second - datetime.Second;

                                minDifference = now1.Minute - datetime.Minute;
                            }


                            else
                            {
                                secDifference = now1.Second + (60 - datetime.Second);

                                minDifference = now1.Minute - datetime.Minute - 1;
                            }



                        }//end if 

                        else if (now1.Minute < datetime.Minute)       //local minute is less than server minute                                               
                        {
                            userMachineFaster = 0;

                            Console.Write("\nServer time exceeds Local time by ");
                            
                            if (datetime.Second >= now1.Second)
                            {
                                secDifference = datetime.Second - now1.Second;

                                minDifference = datetime.Minute - now1.Minute;
                            }


                            else
                            {
                                secDifference = (60 - now1.Second) + datetime.Second;

                                minDifference = datetime.Minute - now1.Minute - 1;
                            }


                        }//end else if              


                        else //minutes are equal. this is usual case
                        {
                            minDifference = 0;

                            if (now1.Second >= datetime.Second) //fetch later
                            {
                                userMachineFaster = 1;

                                Console.Write("\nLocal time exceeds Server time by ");

                                secDifference = now1.Second - datetime.Second;

                            }//end if

                            else                               //fetch earlier
                            {
                                userMachineFaster = 0;

                                Console.Write("\nServer time exceeds Local time by ");

                                secDifference = datetime.Second - now1.Second;

                            }//end else


                        }//end else


                        Console.Write(minDifference + " minutes " + secDifference + " seconds\n");

                        indicate = 1;

                    }


                    //tw = File.AppendText(symbol + ".txt");

                    //Console.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                    //tw.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                    //Console.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                    //tw.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                    //tw.Close();


                }


            }
        }

        private void SmartServer_UpdateBidAsk(string symbol, int row, int nrows, double bid, double bidsize, double ask, double asksize)
        {
            if (row == 0 && LastQuote != null) // обновить котировку. update is detected ONLY if 'symbol' corresponds to last quote symbol
            {
              //  if (LastQuote.Code == symbol)
                {
                    LastQuote.UpDate(ask, asksize, bid, bidsize);

                    tw = File.AppendText("BidAskSpread_Updates.txt");


                    if (symbol == "RTS-6.13_FT")
                        {
                            currBestBid_RTS613 = bid;
                            currBestAsk_RTS613 = ask;

                            if (currBestAsk_RTS913 != -1) // can update 613-913 buy spread now
                            {
                                BuySpread_613_913 = currBestAsk_RTS913 - currBestBid_RTS613;
                                BuyUpdatedRTS = 1;                                
                            }

                            if (currBestBid_RTS913 != -1)  //can update 613-913 sell spread now
                            {
                                SellSpread_613_913 = currBestBid_RTS913 - currBestAsk_RTS613;
                                SellUpdatedRTS = 1;
                            }
                                               
                        if(BuyUpdatedRTS == 1 && SellUpdatedRTS == 1)
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]: Buy Spread [RTS-6.13/RTS-9.13] = " + BuySpread_613_913 + "    Sell Spread [RTS-6.13/RTS-9.13] = " + SellSpread_613_913); 
                   
                        }

               else if (symbol == "RTS-9.13_FT")
                       {
                           currBestBid_RTS913 = bid;
                           currBestAsk_RTS913 = ask;

                           if (currBestBid_RTS613 != -1) // can update 613-913 buy spread now
                           {
                               BuySpread_613_913 = currBestAsk_RTS913 - currBestBid_RTS613;
                               BuyUpdatedRTS = 1;
                           }

                           if (currBestAsk_RTS613 != -1)  //can update 613-913 sell spread now
                           {
                               SellSpread_613_913 = currBestBid_RTS913 - currBestAsk_RTS613;
                               SellUpdatedRTS = 1;
                           }

                        if (BuyUpdatedRTS == 1 && SellUpdatedRTS == 1)
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]: Buy Spread [RTS-6.13/RTS-9.13] = " + BuySpread_613_913 + "    Sell Spread [RTS-6.13/RTS-9.13] = " + SellSpread_613_913); 
                   
                   
                       }

              else if (symbol == "SBRF-6.13_FT")
                      {
                          currBestBid_SBRF = bid;
                          currBestAsk_SBRF = ask;

                          if (currBestBid_SBER != -1) // can update SBRF-SBER buy spread now
                          {
                              BuySpread_SBRF_SBER = currBestAsk_SBRF - currBestBid_SBER * 100;
                              BuyUpdatedSB = 1;                           
                          }

                          if (currBestAsk_SBER != -1)  //can update 613-913 sell spread now
                          {
                              SellSpread_SBRF_SBER = currBestBid_SBRF - currBestAsk_SBER * 100;
                              SellUpdatedSB = 1;                             
                          }

                          if (BuyUpdatedSB == 1 && SellUpdatedSB == 1)
                          tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]: Buy Spread [SBRF-6.13/SBER] = " + BuySpread_SBRF_SBER + "    Sell Spread [SBRF-6.13/SBER] = " + SellSpread_SBRF_SBER); 
                  
                      }
                        
              else if (symbol == "SBER")
                    {
                        currBestBid_SBER = bid;
                        currBestAsk_SBER = ask;

                        if (currBestAsk_SBRF != -1)  //can update 613-913 buy spread now
                        {
                            BuySpread_SBRF_SBER = currBestAsk_SBRF - currBestBid_SBER * 100;
                            BuyUpdatedSB = 1;
                        }

                        if (currBestBid_SBRF != -1) // can update SBRF-SBER sell spread now
                        {
                            SellSpread_SBRF_SBER = currBestBid_SBRF - currBestAsk_SBER * 100;
                            SellUpdatedSB = 1;
                        }


                        if (BuyUpdatedSB == 1 && SellUpdatedSB == 1)
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]: Buy Spread [SBRF-6.13/SBER] = " + BuySpread_SBRF_SBER + "    Sell Spread [SBRF-6.13/SBER] = " + SellSpread_SBRF_SBER); 
                  

                    }


                    tw.Close();






                    //tw = File.AppendText(symbol + ".txt");
                                        
                    //Console.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                    //tw.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                    //Console.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                    //tw.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                    //tw.Close();

                }
            }
        }


        private void SmartServer_AddBar(int row, int nrows, string symbol, StClientLib.StBarInterval interval, System.DateTime datetime, double open, double high, double low, double close, double volume, double open_int)
        {
            //if (datetime > ToDateTimePicker.Value)  // добавить новый бар в список
            //{                

            //sw1.Start();



            InfoBars.Add(new Bar(symbol, datetime, open, high, low, close, volume));       //datetime is system time (Baku on developer system, Moscow on customer system)                                     

            if (symbol == "SBRF-6.13_FT")
            {
                CollectedCloseList_SBRF.Add(close);
                collectedNum_SBRF++;
                Console.WriteLine("close value " + collectedNum_SBRF + " for SBRF added: " + close);
            }
       else if (symbol == "SBER")
            {
                CollectedCloseList_SBER.Add(close);
                collectedNum_SBER++;
                Console.WriteLine("close value " + collectedNum_SBER + " for SBER added: " + close);
            }




            /*

            tw = File.AppendText(symbol + ".txt");

            Console.WriteLine(InfoBars.Last().ToString());
            tw.WriteLine(InfoBars.Last().ToString());
            //tw.Close();

            if (currBar == 1)  //current bar, need to assign o0
            {
                Console.WriteLine("This was Current Bar");

                o0_value = open;

                currBar = 0;

                sw1.Stop();
                Console.WriteLine("o2 received in " + sw1.ElapsedMilliseconds + " msec");

            }
            else if (currBar == 0)  //last bar, need to assign o1 and c1
            {
                Console.WriteLine("This was Last Bar");

                o1_value = open;
                c1_value = close;

                currBar = 1;

                sw1.Stop();
                Console.WriteLine("o1 and c1 received in " + sw1.ElapsedMilliseconds + " msec");
                sw1.Start();

                // and the algorithm may be implemented over here 
                // because there is no event to be received
                //
                // buy and sell cases are disjoint i.e. cannot occur simultaneously, so 
                // for any given bar we will have either sell or buy

                if (o1_value - c1_value > -1000 && c1_value - o0_value >= 20)
                {
                    //open buy order and close at end of current bar                                             

                    SmartServer.PlaceOrder("BP12829-RF-02",
                                           "RTS-6.13_FT",
                                           StOrder_Action.StOrder_Action_Buy,
                                           StOrder_Type.StOrder_Type_Market,
                                           StOrder_Validity.StOrder_Validity_Day,
                                           0,
                                           1,
                                           0,
                                           cookieInit++);

                    Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");
                    tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");

                    buyDone = 1;
                }

                else if (c1_value - o1_value > -1000 && o0_value - c1_value >= 20)
                {
                    //open sell order and close at end of current bar

                    SmartServer.PlaceOrder("BP12829-RF-02",
                                           "RTS-6.13_FT",
                                           StOrder_Action.StOrder_Action_Sell,
                                           StOrder_Type.StOrder_Type_Market,
                                           StOrder_Validity.StOrder_Validity_Day,
                                           0,
                                           1,
                                           0,
                                           cookieInit++);

                    Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");
                    tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");

                    sellDone = 1;
                }

                else
                {
                    Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] NO CONDITION HAS BEEN SATISFIED. NO BUY/SELL PERFORMED");
                    tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] NO CONDITION HAS BEEN SATISFIED. NO BUY/SELL PERFORMED");
                }


                sw1.Stop();
                Console.WriteLine("Decision done in " + sw1.ElapsedMilliseconds + " msec");
                sw1.Reset();

            }//end else if

            tw.Close();

            */

            /*
                     if (closeFlag == 1)  //add this bar to close value checkouts
                     {   twclose.WriteLine("Close Value (C0): " + close); closeReady = 1; }

                     if (openFlag == 1)  //add this bar to open value checkouts
                     {   twopen.WriteLine("Open Value (O0): " + open);    openReady = 1; }
            */

            //}

            /*
            if (row == nrows - 1) // если пришёл последний бар в запросе
            {
                if (InfoBars.Count == 0 || datetime > ToDateTimePicker.Value) // если время последнего бара больше выбранного
                    try
                    {
                        DateTime dtFrom = (InfoBars.Count == 0 ? DateTime.Now : InfoBars.Last().Clock.AddMinutes(-60));
                        Writers.WriteLine("Enegy", "log", "{0} GetBars {1}:{2} From {3}", DateTime.Now, SymbolTextBox.Text, GetInterval, dtFrom);
                        // запросить 500 баров начиная с последнего в списке
                        SmartServer.GetBars(SymbolTextBox.Text, GetInterval, dtFrom, 500);
                    }
                    catch (Exception Error)
                    {
                        Writers.WriteLine("Enegy", "log", "{0} Ошибка в GetBars {1}", DateTime.Now, Error.Message);
                    }
                else
                    new Thread(ThreadBarsSave).Start(); // иначе, считать, что получены все и сохранить список баров
            }

            */
        }


        private void SmartServer_AddPortfolio(int row, int nrows, string portfolioName, string portfolioExch, StClientLib.StPortfolioStatus portfolioStatus)
        {
            tw = File.AppendText("RTS-6.13_FT.txt");
            Console.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));
            tw.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));
            tw.Close();

            // доступен счёт
            if (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker) // работаем только StPortfolioStatus_Broker
            {

                //  Console.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));

                if (InfoPortfolios.IndexOf(portfolioName) == -1) // если данный счёт не известен то запомним
                {
                    InfoPortfolios.Add(portfolioName);
                    //  if (PortfoliosComboBox.SelectedIndex == -1 && PortfoliosComboBox.Items.Count > 0)
                    //      PortfoliosComboBox.SelectedIndex = 0;
                }
                try
                {
                    SmartServer.ListenPortfolio(portfolioName); // подпишемся на прослушку портфеля
                }
                catch (Exception Error)
                {
                    Console.WriteLine("Error occured in ListenPortfolio");
                }
                /*  try
                  {
                      SmartServer.GetMyClosePos(portfolioName); // запросить закрытые позиции
                  }
                  catch (Exception Error)
                  {
                      Writers.WriteLine("Enegy", "log", "{0} Ошибка в GetMyClosePos {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                  }
                  try
                  {
                      SmartServer.GetMyOrders(0, portfolioName); // запросить все приказы по счёту
                  }
                  catch (Exception Error)
                  {
                      Writers.WriteLine("Enegy", "log", "{0} Ошибка в GetMyOrders {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                  }
                  try
                  {
                      SmartServer.GetMyTrades(portfolioName); // запросить все сделки по счёту
                  }
                  catch (Exception Error)
                  {
                      Writers.WriteLine("Enegy", "log", "{0} Ошибка в GetMyTrades {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                  }
                */
            }
        }

        private void SmartServer_SetPortfolio(string portfolio, double cash, double leverage, double comission, double saldo)
        {
            tw = File.AppendText("RTS-6.13_FT.txt");

            Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]" + " Portfolio:" + portfolio + " Cash:" + cash + " Comission:" + comission + " Saldo:" + saldo + " Leverage:" + leverage);
            tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]" + " Portfolio:" + portfolio + " Cash:" + cash + " Comission:" + comission + " Saldo:" + saldo + " Leverage:" + leverage);
            tw.Close();
        }

        private void SmartServer_OrderSucceeded(int cookie, string orderid)
        {
            Console.WriteLine("\nOrder succeeded. Order cookie: " + cookie + " Order ID: " + orderid);
        }
        private void SmartServer_OrderFailed(int cookie, string orderid, string reason)
        {
            Console.WriteLine("\nOrder failed. Order cookie: " + cookie + " Order ID: " + orderid + "Reason: " + reason);
        }




        ///////////////////////////////////////////////////////////////////////////


        private void UpDateQuote()                   ///supposed to change GUI labels. Not needed in console application
        {
            /*
              if (LastQuoteLabel.InvokeRequired) // проверка на главный поток
                  LastQuoteLabel.BeginInvoke(new System.Windows.Forms.MethodInvoker(UpDateQuote));
              else
              {   // Обновить информацию по инструменту
                  LastAskLabel.Text = "Ask: " + LastQuote.Ask + " (" + LastQuote.AskVolume + ")";
                  LastBidLabel.Text = "Bid: " + LastQuote.Bid + " (" + LastQuote.BidVolume + ")";
                  LastLabel.Text = LastQuote.LastClock.ToLongTimeString() + " " + LastQuote.LastPrice + " (" + LastQuote.LastVolume + ") -> " + (LastQuote.LastAction == StOrder_Action.StOrder_Action_Buy ? "B" : LastQuote.LastAction == StOrder_Action.StOrder_Action_Sell ? "S" : LastQuote.LastAction.ToString());
                  LastQuoteLabel.Text = "Status: " + LastQuote.Status;
              }
             */
        }



        private void ListenSymbol(string Code)
        {
            if (SmartServer.IsConnected())
            {
                try
                {
                    Console.WriteLine("[" + Code + "] subscribed for Ticks ", "log", "{0} Listen: {1}, {2}", DateTime.Now, "Ticks", Code);
                    SmartServer.ListenTicks(Code);    // подписаться на получение всех сделок
                    try
                    {
                        Console.WriteLine("[" + Code + "] subscribed for BidAsks", "log", "{0} Listen: {1}, {2}", DateTime.Now, "BidAsks", Code);
                        SmartServer.ListenBidAsks(Code);  // подписаться на получение стакана
                        try
                        {
                            Console.WriteLine("[" + Code + "] subscribed for Quotes", "log", "{0} Listen: {1}, {2}", DateTime.Now, "Quotes", Code);
                            SmartServer.ListenQuotes(Code);   // подписаться на получение котировок по инструменту
                        }
                        catch (Exception Error)
                        {
                            Console.WriteLine("Ошибка при подписке на котировку");
                        }
                    }
                    catch (Exception Error)
                    {
                        Console.WriteLine("Ошибка при подписке на стакан");
                    }
                }
                catch (Exception Error)
                {
                    Console.WriteLine("Ошибка при подписке на сделки");
                }
            }
        }



        private void InvertPlaceOrder()
        {

            while (true)
            {
                if (userMachineFaster == 1)                // trade close time right at 18.44.59
                { deltaClose = 0; deltaDef2 = 12; }
                else                                       // trade close time 5 minutes before 18.44.59
                { deltaClose = -5; deltaDef2 = 11; }


                if (DateTime.Now.Minute == (5 * deltaDef2 - 1) && DateTime.Now.Second == 59 ||
                    DateTime.Now.Hour == 18 && DateTime.Now.Minute == (44 + deltaClose) && DateTime.Now.Second == 59)
                {

                    tw = File.AppendText("RTS-6.13_FT.txt");

                    if (buyDone == 1)
                    {
                        //sell

                        SmartServer.PlaceOrder("BP12829-RF-02",
                                                 "RTS-6.13_FT",
                                                 StOrder_Action.StOrder_Action_Sell,
                                                 StOrder_Type.StOrder_Type_Market,
                                                 StOrder_Validity.StOrder_Validity_Day,
                                                 0,
                                                 1,
                                                 0,
                                                 cookieInit++);

                        Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");

                        buyDone = 0;  //reset                        
                    }

                    if (sellDone == 1)
                    {

                        //buy

                        SmartServer.PlaceOrder("BP12829-RF-02",
                                                 "RTS-6.13_FT",
                                                 StOrder_Action.StOrder_Action_Buy,
                                                 StOrder_Type.StOrder_Type_Market,
                                                 StOrder_Validity.StOrder_Validity_Day,
                                                 0,
                                                 1,
                                                 0,
                                                 cookieInit++);

                        Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");

                        sellDone = 0; //reset
                    }


                    tw.Close();

                    while (DateTime.Now.Second == 59) { }

                }//end if time    

                Thread.Sleep(50);

            }//end while

        }//end function



        private void CollectCloseValues()
        { 
           
          if (userMachineFaster == 1) // fetch bars 5 minuntes after xx.00.00
              deltaDef = 11;
          else                        // fetch bars right at xx.00.00
              deltaDef = 12;


        while(barsToGet > 0)
             {

                 if (DateTime.Now.Second == 20) //assume that local machine runs faster than server

                 {
                                          
                     SmartServer.GetBars("SBRF-6.13_FT", StBarInterval.StBarInterval_1Min, DateTime.Now, 1);

                     SmartServer.GetBars("SBER", StBarInterval.StBarInterval_1Min, DateTime.Now, 1);

                     barsToGet--;

                     while (DateTime.Now.Second == 20) { } //wait until next second starts


                 }//end if


                 Thread.Sleep(50);
        
             }//end while


        collectedNum_SBRF = 0; //reset
        collectedNum_SBER = 0;


        //here we calculate AVG and STDEV of
        //and make a trade order if necessary conditions are satisfied and terminate this thread

        int i;
        
        for (i = 0; i < CollectedCloseList_SBRF.Count; i++)

             CollectedCloseList_SBRF[i] = CollectedCloseList_SBRF[i] - CollectedCloseList_SBER[i] * 100;

        double avg_simple = MathAvg     (CollectedCloseList_SBRF);

        double stdev      = StdDeviation(CollectedCloseList_SBRF);

        //values ready

        //process them here depending on the strategy parameters








        CollectedCloseList_SBRF.Clear();
        CollectedCloseList_SBER.Clear();

        }//end function


        private void TraceMorningTime()
        {

            while (true)
            {
                if (DateTime.Now.Hour == 11 && DateTime.Now.Minute == 0 && DateTime.Now.Second == 20)  //assume that local machine runs faster than server
                {
                    startedFlag = 0;
                    break;
                }
            
            }

                    
        }
        

        private double MathAvg(List<double> closesList)
        {
            int i = 0;

            double sum = 0;

            for (i = 0; i < closesList.Count; i++)

                sum = sum + closesList[i];

            return sum/closesList.Count;        
        
        }

        private double StdDeviation(List<double> closesList)
        {

            int i = 0;

            double mathavg = MathAvg(closesList);

            double sum = 0;
            double stdv = 0;

            for (i = 0; i < closesList.Count; i++)

                closesList[i] = (closesList[i] - mathavg) * (closesList[i] - mathavg);

            for (i = 0; i < closesList.Count; i++)

                sum = sum + closesList[i];

            stdv = Math.Sqrt(sum/closesList.Count);

            return stdv;        
        
        }
        

        static void Main(string[] args)
        {

            newTestClass ntc = new newTestClass();
            ntc.doActions();
            
        }


    } /* end class newTestClass */



    public class Quote
    {
        public event EventHandler EventUpDate;
        public delegate void EventHandler();

        private string InfoCode;
        private double InfoAsk;
        private double InfoBid;
        private int InfoAskVolume;
        private int InfoBidVolume;
        private int InfoStatus;
        private long InfoLastNo;
        private double InfoLastPrice;
        private int InfoLastVolume;
        private DateTime InfoLastClock;
        private StClientLib.StOrder_Action InfoLastAction;

        public Quote(string Code, DateTime Clock, double Last, double Volume, int Status, EventHandler OnEventUpDate)
        {
            InfoCode = Code;
            InfoAsk = 0.0d;
            InfoBid = 0.0d;
            InfoLastNo = 0;
            InfoAskVolume = 0;
            InfoBidVolume = 0;
            InfoStatus = Status;
            InfoLastPrice = Last;
            InfoLastVolume = (int)Volume;
            InfoLastClock = Clock;
            if (OnEventUpDate != null)
                EventUpDate += new EventHandler(OnEventUpDate);
            new Thread(ThreadUpdate).Start();
        }

        private void ThreadUpdate()
        {
            if (EventUpDate != null)
                EventUpDate();
        }

        public void UpDate(int Status)
        {
            InfoStatus = Status;
            new Thread(ThreadUpdate).Start();
        }
        public void UpDate(double Ask, double AskVolume, double Bid, double BidVolume)                                     /* used by SmartServer_UpdateBidAsk listener */
        {
            InfoAsk = Ask;
            InfoBid = Bid;
            InfoAskVolume = (int)AskVolume;
            InfoBidVolume = (int)BidVolume;
            new Thread(ThreadUpdate).Start();
        }
        public void UpDate(DateTime Clock, double Price, double Volume, long LastNo, StClientLib.StOrder_Action Action)    /* used by SmartServer_AddTick listener     */
        {
            InfoLastNo = LastNo;
            InfoLastClock = Clock;
            InfoLastPrice = Price;
            InfoLastVolume = (int)Volume;
            InfoLastAction = Action;
            new Thread(ThreadUpdate).Start();
        }


        public string Code { get { return InfoCode; } }
        public double Ask { get { return InfoAsk; } }
        public double Bid { get { return InfoBid; } }
        public int AskVolume { get { return InfoAskVolume; } }
        public int BidVolume { get { return InfoBidVolume; } }
        public int Status { get { return InfoStatus; } }
        public long LastNo { get { return InfoLastNo; } }
        public double LastPrice { get { return InfoLastPrice; } }
        public int LastVolume { get { return InfoLastVolume; } }
        public DateTime LastClock { get { return InfoLastClock; } }
        public StClientLib.StOrder_Action LastAction { get { return InfoLastAction; } }
    }

    /* end class Quote */

    public class Tiker
    {
        private string sCode;
        private string sShortName;
        private string sLongName;
        private double dStep;
        private double dStepPrice;
        private int iDecimals;
        private string sSecExtId;
        private string sSecExchName;
        private System.DateTime dtExpiryDate;
        private double dDaysBeforeExpiry;
        private double dStrike;

        public Tiker(string code, string shortname, string longname, double step, double stepprice, double decimals, string sec_ext_id, string sec_exch_name, System.DateTime expiryDate, double daysbeforeexpiry, double strike)
        {
            sCode = code;
            sShortName = shortname;
            sLongName = longname;
            dStep = step;
            dStepPrice = stepprice;
            iDecimals = (int)decimals;
            sSecExtId = sec_ext_id;
            sSecExchName = sec_exch_name;
            dtExpiryDate = expiryDate;
            dDaysBeforeExpiry = daysbeforeexpiry;
            dStrike = strike;
        }

        public double ToMoney(double Punkts)
        {
            return dStepPrice / dStep * Punkts;
        }

        public override string ToString()
        {
            CultureInfo ci = new CultureInfo("en-us");
            return "[symbol = " + sCode + "]\n[strike = " + dStrike + "]\n[Punkt = " + dStepPrice + "]\n[Step = " + dStep.ToString("G", ci) + "]\n[Decimals = " + iDecimals + "]\n[Money = " + ToMoney(1) + "]\n[shortname = " + sShortName + "]\n[expirydate = " + dtExpiryDate.ToShortDateString() + "] (" + (int)dDaysBeforeExpiry + " days before expiry)";
        }

        public string Code { get { return sCode; } }
        public string ShortName { get { return sShortName; } }
        public string LongName { get { return sLongName; } }
        public double Step { get { return dStep; } }
        public double StepPrice { get { return dStepPrice; } }
        public int Decimals { get { return iDecimals; } }
        public string SecExtId { get { return sSecExtId; } }
        public string SecExchName { get { return sSecExchName; } }
        public System.DateTime ExpiryDate { get { return dtExpiryDate; } }
        public double DaysBeforeExpiry { get { return dDaysBeforeExpiry; } }
    }

    /* end class Tiker */

    public class Bar
    {
        public enum Type { Open, Max, Min, Close, AVG }

        private string InfoCode;
        private System.DateTime InfoClock;
        private double InfoOpen;
        private double InfoHigh;
        private double InfoLow;
        private double InfoClose;
        private double InfoVolume;

        public Bar(string code, System.DateTime clock, double open, double high, double low, double close, double volume)
        {
            InfoCode = code;
            InfoClock = clock;
            InfoOpen = open;
            InfoHigh = high;
            InfoLow = low;
            InfoClose = close;
            InfoVolume = volume;
        }

        public string Code { get { return InfoCode; } }
        public DateTime Clock { get { return InfoClock; } }
        public double Open { get { return InfoOpen; } }
        public double High { get { return InfoHigh; } }
        public double Low { get { return InfoLow; } }
        public double Close { get { return InfoClose; } }
        public double Volume { get { return InfoVolume; } }
        public double GetBy(Type type)
        {
            return (type == Type.Open ? InfoOpen : type == Type.Max ? InfoHigh : type == Type.Min ? InfoLow : type == Type.Close ? InfoClose : type == Type.AVG ? (InfoLow + InfoHigh) / 2.0d : 0.0d);
        }

        public override string ToString()
        {
            return "\nBar[" + InfoCode + "] " + InfoClock.ToString() + " Open:" + InfoOpen + " High:" + InfoHigh + " Low:" + InfoLow + " Close:" + InfoClose + " Volume:" + InfoVolume;
        }
    }

    /* end class Bar */



} /* end namespace */