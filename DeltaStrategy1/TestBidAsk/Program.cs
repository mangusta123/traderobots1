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
        private StreamWriter tw;
        private StreamWriter twclose;
        private StreamWriter twopen;

        private Stopwatch sw1;


        private int entrycount = 0;
        private int dayOpen;
        private int hourOpen;
        private int minuteOpen;
        private int latestMinute;
        private double accumulatorBuy = 0;
        private double accumulatorSell = 0;
        private int startflag = 0;        
        private double traceClose = 0;
        private int traceHour = 0;
        private int counter = 0;
        private int ultimateMinCounter = 0;
        private int resetflag = 1;
        private int neededCloseNumber = 29;
        List<double> closeList = new List<double>();

        double stdev_close;
        double z_score;

        //constants taken from config file
        int posLimitMval = 200;
        int negLimitMval = -200;
        int posLimitZscr = 2;
        int negLimitZscr = -2;
        int tradeDuration = 10;

        //
        int terminateThread = 0;
        int serverHour;
        int serverMin;
        int serverSec;

        double currOpen = 0;

        double prevClose = 0;
        double currClose = 0;

        double prevDelta = 0;
        double currDelta = 0;

        double Kval, Lval, Mval;

        int deltaIterator = 0;
               


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


        double BuySpread_SBRF_SBER = 0;
        double SellSpread_SBRF_SBER = 0;

        double BuySpread_613_913 = 0;
        double SellSpread_613_913 = 0;

        double currBestBid_RTS613 = -1;
        double currBestAsk_RTS613 = -1;
        double currBestBid_RTS913 = -1;
        double currBestAsk_RTS913 = -1;
        double currBestBid_SBER = -1;
        double currBestAsk_SBER = -1;
        double currBestBid_SBRF = -1;
        double currBestAsk_SBRF = -1;

        int BuyUpdatedRTS = 0;
        int SellUpdatedRTS = 0;
        int BuyUpdatedSB = 0;
        int SellUpdatedSB = 0;

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


                File.Delete("stocksharplog.txt");
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


                        Console.WriteLine("Connection established.");


                        try
                        {                                                       

                            terminateThread = 1;
                            new Thread(ConfigReaderRoutine).Start();


                            SmartServer.GetPrortfolioList();                                     /*get list of portfolios available for current login */

                            //     SmartServer.ListenPortfolio("BP12829-RF-02");                 /*receive notifications about portfolio changes      */

                            SmartServer.GetSymbols();                                     /* get symbols list                                  */


                            //     we need to get two bars for last two hours every hour at xx.00.00 and 
                            //     o0 <= open  value of the very last bar 
                            //     o1 <= open  value of the bar before the very last bar
                            //     c1 <= close value of the bar before the very last bar
                            //     and then apply the suggested algorithm

                            //     hence 
                            //     the first  invocation of AddBar should fill in o0  
                            // and the second invocation of AddBar should fill in o1 and c1





                            //     SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);  

                           // ListenSymbol("RTS-6.13_FT");                                  /* subscribe for ticks, bids & quotes of the         */

                            ListenSymbol("RTS-6.13_FT");

                           // ListenSymbol("SBRF-6.13_FT");

                           // ListenSymbol("SBER");


                            /* specified symbol                                  */
                            //     new Thread(DefinePreciseClose).Start();
                            //     new Thread(DefinePreciseOpen) .Start();


                            //     this thread is going to detect xx.59.59 and revert order if necessary

                            //new Thread(InvertPlaceOrder).Start();

                            //     current thread is going to detect time xx.00.00                                                          

                            /*
                                                        while (true)
                                                        {
                                                            if (userMachineFaster == 1) // fetch bars 5 minuntes after xx.00.00
                                                                deltaDef = 11;
                                                            else                        // fetch bars right at xx.00.00
                                                                deltaDef = 12;


                                                            if (DateTime.Now.Minute == (60 - 5 * deltaDef) && DateTime.Now.Second == 00)
                                                            {

                                                                SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);

                                                                while (DateTime.Now.Second == 00) { } //wait until second becomes 01 


                                                            }//end if


                                                            Thread.Sleep(50);

                                                        }//end while                      

                            */
                            Console.ReadKey();


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

            if ((symbol == "RTS-6.13_FT"))// || (symbol == "RTS-9.13_FT") || (symbol == "SBRF-6.13_FT") || (symbol == "SBER"))//|| symbol == "RTSo-6.13_FT" || symbol == "RTSc-6.13_FT")
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
                if (symbol == "RTS-6.13_FT")// || symbol == "RTS-9.13_FT" || symbol == "SBRF-6.13_FT" || symbol == "SBER")// || symbol == "RTSc-6.13_FT")
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

                            if (indicate == 0)
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

                            if (indicate == 0)
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

                                if (indicate == 0)
                                    Console.Write("\nLocal time exceeds Server time by ");

                                secDifference = now1.Second - datetime.Second;

                            }//end if

                            else                               //fetch earlier
                            {
                                userMachineFaster = 0;

                                if (indicate == 0)
                                    Console.Write("\nServer time exceeds Local time by ");

                                secDifference = datetime.Second - now1.Second;

                            }//end else


                        }//end else


                        Console.Write(minDifference + " minutes " + secDifference + " seconds\n");

                        indicate = 1;

                    }//end if indicate=0


                    /////////////////////////////////////////working part///////////////////////////

                    serverSec = datetime.Second;	

                    if (startflag == 0)                                          //save the minute at launch, so we should skip it 
                    {                                                          //and start logging at next minute.

                        if (datetime.Minute == DateTime.Now.Minute)
                        {

                            dayOpen = datetime.Day;

                            hourOpen = datetime.Hour;

                            minuteOpen = datetime.Minute;

                            latestMinute = datetime.Minute;

                            Console.WriteLine("the very first trade item. launch minute = " + minuteOpen);

                            startflag = 1;

                        }


                    }//end if

                    if (startflag == 1 && (datetime.Day != dayOpen || datetime.Hour != hourOpen || datetime.Minute != minuteOpen))        //not the minute at launch, so perform logging.
                    //this launch minute will come up again every month. 
                    {




                        if (datetime.Minute != latestMinute) //if current minute is different from latest minute, we
                        //reset accumulator and change latest minute
                        {
                            prevClose = currClose;                    //save down close value at every minute start
                            currClose = traceClose;

                            currOpen = price;
                            serverHour = datetime.Hour;
                            serverMin = datetime.Minute;

                            closeList.Add(traceClose);
                            counter++;
                            ultimateMinCounter++;
                            Console.WriteLine("counter=" + counter);


                            if (latestMinute != minuteOpen)           //do not have to output for minute at launch
                            {
                                Console.WriteLine("new minute started. [" + datetime.AddMinutes(latestMinute - datetime.Minute).Hour + ":" + datetime.AddMinutes(latestMinute - datetime.Minute).Minute + ":" + "00] accumulated amount for this minute: BuyVolume = " + accumulatorBuy + " SellVolume = " + accumulatorSell);

                                tw = File.AppendText("stocksharplog.txt");
                                tw.WriteLine("[" + datetime.Hour + ":" + datetime.Minute + ":" + "00] new minute started. accumulated amount for last minute: BuyVolume = " + accumulatorBuy + " SellVolume = " + accumulatorSell);
                                tw.Close();
                                //this.AddInfoLog("not launch minute. new minute started.");


                                //at second iteration of this branch we can calculate K, L, M

                                if (deltaIterator < 2)
                                    deltaIterator++;

                                prevDelta = currDelta;
                                currDelta = (int)(accumulatorBuy - accumulatorSell);

                                if (deltaIterator == 2)
                                {
                                    Kval = (currDelta / prevDelta - 1) * 100;
                                    Lval = (currClose / prevClose - 1) * 100000;

                                    Kval = Math.Round(Kval, 0, MidpointRounding.AwayFromZero);
                                    Lval = Math.Round(Lval, 0, MidpointRounding.AwayFromZero);
                                    Mval = Kval - Lval;

                                    Console.WriteLine("Close=" + currClose + " K=" + Kval + " L=" + Lval + " M=" + Mval);

                                    tw = File.AppendText("stocksharplog.txt");
                                    tw.WriteLine("Close=" + currClose + " K=" + Kval + " L=" + Lval + " M=" + Mval);
                                    tw.Close();

                                }


                            }

                            else

                            { Console.WriteLine("<<<<<new minute started>>>>>"); Console.WriteLine("Close=" + currClose); }


                            if (counter >= neededCloseNumber)
                            {

                                //calculate stdev_close and z_score and see if they satisfy order conditions

                                //for each order open we create a separate thread which is going to monitor it until order close 

                                //stdev_close = StdDeviation(closeList);					 						 
                                //z_score     = (closeList[neededCloseNumber-1] - MathAvg(closeList))/stdev_close; 

                                stdev_close = StdDeviationNew(closeList);
                                z_score = (closeList[closeList.Count() - 1] - MathAvgNew(closeList)) / stdev_close;

                                stdev_close = Math.Round(stdev_close, 4, MidpointRounding.AwayFromZero);
                                z_score = Math.Round(z_score, 2, MidpointRounding.AwayFromZero);

                                Console.WriteLine("STDEV = " + stdev_close + " z_score = " + z_score);

                                tw = File.AppendText("stocksharplog.txt");
                                tw.WriteLine("STDEV = " + stdev_close + " z_score = " + z_score);
                                tw.Close();


                                if (z_score > posLimitZscr && Mval > posLimitMval)       //condition for BUY
                                {
                                    new Thread(TradeBuyThreadRoutine).Start(ultimateMinCounter);  //we should pass open value and  ultimateMinCounter to this thread
                                    
                                    tw = File.AppendText("stocksharplog.txt");
                                    tw.WriteLine("BUY now!!!");
                                    tw.Close();
                                }

                                else if (z_score > negLimitZscr && Mval < negLimitMval)	 //condition for SELL		 
                                {
                                    new Thread(TradeSellThreadRoutine).Start(ultimateMinCounter);  //we should pass open value and ultimateMinCounter to this thread
                                    
                                    tw = File.AppendText("stocksharplog.txt");
                                    tw.WriteLine("SELL now!!!");
                                    tw.Close();
                                }


                                //remove which capacity reaches 100
                                if (counter == 100)
                                {
                                    closeList.RemoveAt(0);
                                    counter--;
                                }

                            }


                            if (action == StOrder_Action.StOrder_Action_Buy)
                            {
                                accumulatorBuy = volume;
                                accumulatorSell = 0;
                            }

                            else if (action == StOrder_Action.StOrder_Action_Sell)
                            {
                                accumulatorSell = volume;
                                accumulatorBuy = 0;
                            }

                            latestMinute = datetime.Minute;

                        }//end if current and last tick minutes are different


                        else
                        {
                            //this.AddInfoLog("minute still goes. adding...");


                            if (action == StOrder_Action.StOrder_Action_Buy)

                                accumulatorBuy = accumulatorBuy + volume;

                            else if (action == StOrder_Action.StOrder_Action_Sell)

                                accumulatorSell = accumulatorSell + volume;


                        }//end else (current and last tick minutes are same)


                    }//end if startflag == 1


                    traceClose = price; //save price at every tick so we can trace the last tick price of minute
                    //this value is saved on opening minute as well			

                    traceHour = datetime.Hour;  //save hour at every tick so we can reset the strategy every 10.00am
                    if (traceHour == 18)
                        resetflag = 0;
			

                    /////////////////////////////////////////////////////////////////////////////////

                    /*
                    //tw = File.AppendText(symbol + ".txt");

                    Console.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                    //tw.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                    Console.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                    //tw.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                    //tw.Close();
                    */

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
                    /*
                    tw = File.AppendText("BidAskSpread_Updates.txt");

					tw.Close();

                    */

                    //tw = File.AppendText(symbol + ".txt");

                    Console.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                    //tw.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                    Console.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                    //tw.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                    //tw.Close();

                }
            }
        }


        private void SmartServer_AddBar(int row, int nrows, string symbol, StClientLib.StBarInterval interval, System.DateTime datetime, double open, double high, double low, double close, double volume, double open_int)
        {
            //if (datetime > ToDateTimePicker.Value)  // добавить новый бар в список
            //{                

            sw1.Start();

            InfoBars.Add(new Bar(symbol, datetime, open, high, low, close, volume));       //datetime is system time (Baku on developer system, Moscow on customer system)                                     

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
                    //    Console.WriteLine("[" + Code + "] subscribed for BidAsks", "log", "{0} Listen: {1}, {2}", DateTime.Now, "BidAsks", Code);
                    //    SmartServer.ListenBidAsks(Code);  // подписаться на получение стакана
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


        private void DefinePreciseClose()
        {       //this thread starts checking close values right at xx.59.59
            //it checks out close value every 100 milliseconds

            File.Delete("CloseValueCheckouts.txt");

            while (true)                          //so that we have total of 10 checkouts per second
            {
                if (DateTime.Now.Minute == 59 && DateTime.Now.Second == 59)
                {
                    closeFlag = 1; //so the file above is populated

                    twclose = File.AppendText("CloseValueCheckouts.txt");  //open once
                    twclose.WriteLine("\n--------TIME: " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                    while (DateTime.Now.Second == 59)
                    {
                        closeReady = 0;
                        SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 1);  //the current bar
                        Thread.Sleep(90);

                        while (closeReady == 0) { }    //race condition 
                        //which may occur due to context switch between assembly lines 
                        //loop: cmp closeReady 0
                        //je loop

                        //but in our case closeReady can never be assigned 0 during context switch
                        //hence race condition is safe                                                      

                    }

                    twclose.Close();
                    closeFlag = 0;//reset
                }

            }//end while

        }//end function


        private void DefinePreciseOpen()
        {                                         //this thread checks open value right at xx.00.00


            File.Delete("OpenValueCheckouts.txt");

            while (true)
            {

                if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    openFlag = 1; //so the file above is populated

                    twopen = File.AppendText("OpenValueCheckouts.txt");
                    twopen.WriteLine("\n--------TIME: " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                    openReady = 0;
                    SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 1);  //get the last bar as early as possible

                    while (openReady == 0) { }         //race condition 
                    //which may occur due to context switch between assembly lines 
                    //loop: cmp openReady 0
                    //je loop

                    //but in our case openReady can never be assigned 0 during context switch
                    //hence race condition is safe

                    twopen.Close();
                    openFlag = 0;//reset


                    while (DateTime.Now.Second == 00) { } //wait until second becomes 01                                  

                }//end if

            }//end while

        }//end function


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


        private double MathAvgNew(List<double> closesList)  //supposed to calculate avg for last 'neededCloseNumber' elements
        {
            int i = 0;

            double sum = 0;

            for (i = closesList.Count() - neededCloseNumber; i < closesList.Count(); i++)

                sum = sum + closesList[i];

            return sum / neededCloseNumber;

        }

        private double StdDeviationNew(List<double> closesList) //supposed to calculate stdev for last 'neededCloseNumber' elements
        {

            int i = 0;

            double tempValA = 0;

            double mathavg = MathAvgNew(closesList);

            double sum = 0;
            double stdv = 0;


            for (i = closeList.Count() - neededCloseNumber; i < closesList.Count(); i++)
            {
                tempValA = (closesList[i] - mathavg) * (closesList[i] - mathavg);

                sum = sum + tempValA;
            }

            double tempValB = sum / neededCloseNumber;


            stdv = Math.Sqrt(tempValB);

            return stdv;

        }


        private void TradeBuyThreadRoutine(object data)
        {
            //created for every trade and lasts a number of minutes specified in config file (tradeDuration)
            Console.WriteLine("BUY now!!!");

            SmartServer.PlaceOrder("BP8602-RF-02",
                                   "RTS-6.13_FT",
                                   StOrder_Action.StOrder_Action_Buy,
                                   StOrder_Type.StOrder_Type_Limit,
                                   StOrder_Validity.StOrder_Validity_Day,
                                   currOpen + 20,
                                   1,
                                   0,
                                   cookieInit++);                       

            int startCount = (int)data;

            while (ultimateMinCounter - startCount < tradeDuration)
            {

                if (serverHour == 23 && serverMin == 49 && serverSec > 30)
                    break;

                Thread.Sleep(100);

            }//end while


            SmartServer.PlaceOrder("BP8602-RF-02",
                                   "RTS-6.13_FT",
                                   StOrder_Action.StOrder_Action_Sell,
                                   StOrder_Type.StOrder_Type_Limit,
                                   StOrder_Validity.StOrder_Validity_Day,
                                   currOpen,
                                   1,
                                   0,
                                   cookieInit++);
                       


        }//end routine


        private void TradeSellThreadRoutine(object data)
        {
            //created for every trade and lasts a number of minutes specified in config file (tradeDuration)
            Console.WriteLine("SELL now!!!");

            SmartServer.PlaceOrder("BP8602-RF-02",
                                   "RTS-6.13_FT",
                                   StOrder_Action.StOrder_Action_Sell,
                                   StOrder_Type.StOrder_Type_Limit,
                                   StOrder_Validity.StOrder_Validity_Day,
                                   currOpen - 20,
                                   1,
                                   0,
                                   cookieInit++);

            int startCount = (int)data;

            while (ultimateMinCounter - startCount < tradeDuration)
            {

                if (serverHour == 23 && serverMin == 49 && serverSec > 30)
                    break;

                Thread.Sleep(100);

            }//end while		


            SmartServer.PlaceOrder("BP8602-RF-02",
                                   "RTS-6.13_FT",
                                   StOrder_Action.StOrder_Action_Buy,
                                   StOrder_Type.StOrder_Type_Limit,
                                   StOrder_Validity.StOrder_Validity_Day,
                                   currOpen,
                                   1,
                                   0,
                                   cookieInit++);

        }//end routine


        private void ConfigReaderRoutine()
        {
            //created at the beginning of OnStarted and continuosly reads config file values in an infinite loop

            while (terminateThread == 1)
            {

                System.IO.StreamReader exfile = new System.IO.StreamReader("conf.ini");

                exfile.ReadLine();

                posLimitMval = Convert.ToInt32(exfile.ReadLine());

                exfile.ReadLine();

                negLimitMval = Convert.ToInt32(exfile.ReadLine());

                exfile.ReadLine();

                posLimitZscr = Convert.ToInt32(exfile.ReadLine());

                exfile.ReadLine();

                negLimitZscr = Convert.ToInt32(exfile.ReadLine());

                exfile.ReadLine();

                tradeDuration = Convert.ToInt32(exfile.ReadLine());

                exfile.ReadLine();

                neededCloseNumber = Convert.ToInt32(exfile.ReadLine());

                exfile.Close();

                //Console.WriteLine("config info: " + posLimitMval + " " + negLimitMval + " " + posLimitZscr + " " + negLimitZscr + " " + tradeDuration + " " + neededCloseNumber);	

                Thread.Sleep(5000);

            }//end while	

        }//end routine






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