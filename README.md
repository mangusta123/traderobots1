traderobots1
==============

Trade robots exploiting three different trading strategies 

These robots are based on COM SmartLab API provided by ITinvest (broker company headquartered 

in Moscow, Russia, more info at http://www.itinvest.ru/) which serves as an intermediary between trader

and Moscow Stock Exchange (http://www.moex.com/)

Its PDF manual can be found in the repository and library can be downloaded from the website below:

http://www.itinvest.ru/software/smartcom/

COM library access is possible through either C++ or C# and due to invokation simplicity I stopped my

choice on C# since we need some extra information (e.g. GUID, CLSID etc.) to make use of COM module

within C++ program whereas in C# it is just the matter of linking COM module by simply choosing it in 

the project properties - by doing so all COM functions become automatically visible across the program

It is required to have an account registered in ITinvest since SmartLab connects to ITinvest server by 

means of ITinvest user credentials

Primary goal of any Trade Robot is to listen (subscribe) to certain stocks/derivatives, receive trade 

information about them (ticks, bid/ask, open/close, time bars), process them and based on the result, 

issue BUY/SELL transaction to the server

Proper GUI can be developed to work with desired stocks/derivatives (similar to the one given together

with SmartCom tutorial on ITinvest website). Programs in the repository are console-based

DeltaStrategy and SpreadStrategy were designed specifically for Moscow Stock Exchange derivative called 

RTS-6.13 and BidAskStrategy makes use of difference between bid/ask values of two derivatives, namely RTS 

and SBERBANK

In order to understand trading strategies it is useful to know some popular trading terminology like:  
-ticks  
-bid/ask  
-open/close and spread  
-bar  
etc.

1.SpreadStrategy is based on permanently checking hourly bars at the start of every hour (at xx.00.00) and 

calculating difference between open/close values of bars for the previous hour and for the one that has just

begun

2.DeltaStrategy is based on regularly preparing a close window, i.e. accumulating a specific number of 

close values from ticks, then calculating STDEV and Z-Score for them (definition of Z-score is given 

in the code)

3.BidAskStrategy resembles DeltaStrategy with one difference that it gets Bid/Ask values from two derivatives

simultaneously and after collecting enough of them, their STDEV values are calculated and depending on the 

result, proper transaction is sent to the server































