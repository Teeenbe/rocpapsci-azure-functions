using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Azure.Identity;

namespace RockPaperScissorsFunctions
{
    public static class RpsCalculateMoves
    {
        [FunctionName("RpsCalculateMoves")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Player move made.");

            // Assign player's move
            int.TryParse(req.Query["playerMove"], out int playerMoveRequest);
            MoveOption playerMove = (MoveOption)playerMoveRequest;

            //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //playerMove = playerMove ?? data?.playerMove;

            // Calculate computer's move
            var computerMove = CalculateComputerMove();

            // Determine winner
            var winner = CalculateWinState(playerMove, computerMove);

            var gameResult = new GameState { PlayerMove = playerMove.ToString(), ComputerMove = computerMove.ToString(), Winner = winner.ToString() };
            var gameResultResponse = Newtonsoft.Json.JsonConvert.SerializeObject(gameResult);

            log.LogInformation($"Player chose: {playerMove}\nComputer chose: {computerMove}\nWinner: {winner}");

            // Publish to Service Bus queue
            var published = await PublishToQueue(gameResultResponse, log);

            return new OkObjectResult(new { PlayerMove = gameResult.PlayerMove, ComputerMove = gameResult.ComputerMove, Winner = gameResult.Winner, WasSaved = published });
        }

        private static async Task<bool> PublishToQueue(string gameResultResponse, ILogger log)
        {
            var wasSuccessful = false;

            ServiceBusClient client;
            ServiceBusSender sender;

            var clientOptions = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpWebSockets
            };

            client = new ServiceBusClient(Environment.GetEnvironmentVariable("RpsBusConnection"), clientOptions);
            sender = client.CreateSender("rockpaperscissorsgameresults");

            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

            if (!messageBatch.TryAddMessage(new ServiceBusMessage($"{gameResultResponse}")))
            {
                log.LogInformation("Game result was too large to add to queue.");
            }

            try
            {
                await sender.SendMessagesAsync(messageBatch);
                log.LogInformation($"Game result published to queue: {gameResultResponse}");
                wasSuccessful = true;
            }
            catch
            {
                log.LogInformation($"Game result could not be published to queue: {gameResultResponse}");
                await sender.DisposeAsync();
                await client.DisposeAsync();
                wasSuccessful = false;
            }
            finally
            {
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }
            return wasSuccessful;
        }

        private static MoveOption CalculateComputerMove()
        {
            MoveOption[] moveOptions = { MoveOption.Rock, MoveOption.Paper, MoveOption.Scissors };

            var rand = new Random();
            var computerMoveIndex = rand.Next(moveOptions.Length);

            return moveOptions[computerMoveIndex];
        }

        private static WinState CalculateWinState(MoveOption playerMove, MoveOption computerMove)
        {
            switch (playerMove)
            {
                case MoveOption.Rock:
                    if (computerMove == MoveOption.Rock)
                    {
                        return WinState.Tie;
                    }
                    if (computerMove == MoveOption.Paper)
                    {
                        return WinState.Computer;
                    }
                    if (computerMove == MoveOption.Scissors)
                    {
                        return WinState.Player;
                    }
                    return WinState.Tie;
                case MoveOption.Paper:
                    if (computerMove == MoveOption.Rock)
                    {
                        return WinState.Player;
                    }
                    if (computerMove == MoveOption.Paper)
                    {
                        return WinState.Tie;
                    }
                    if (computerMove == MoveOption.Scissors)
                    {
                        return WinState.Computer;
                    }
                    return WinState.Tie;
                case MoveOption.Scissors:
                    if (computerMove == MoveOption.Rock)
                    {
                        return WinState.Computer;
                    }
                    if (computerMove == MoveOption.Paper)
                    {
                        return WinState.Player;
                    }
                    if (computerMove == MoveOption.Scissors)
                    {
                        return WinState.Tie;
                    }
                    return WinState.Tie;
                default:
                    return WinState.Tie;
            }
        }
    }

    public enum MoveOption
    {
        Rock = 1,
        Paper = 2,
        Scissors = 3,
    }

    public enum WinState
    {
        Tie = 0,
        Player = 1,
        Computer = 2,
    }

    public class GameState
    {
        public string PlayerMove;
        public string ComputerMove;
        public string Winner;
    }
}
