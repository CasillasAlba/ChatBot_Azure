using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System;


namespace BestBot
{
    public class MainDialog : ComponentDialog
    {

        protected readonly ILogger _logger;    
        private readonly LuisExRecognizer _luisExRecognizer;
        private const string RepromptError =  "Se ha introducido un dato incorrecto. Por favor, reformule su reserva";
        private const string RepromptMsgNoCom =  "Lo siento, no disponemos de la información de esta facultad en la ciudad indicada :(";
        private const string RepromptMsgHora = "Lo siento, esa hora no es correcta o ya esta reservada";

        string ciudad_correcta;
        string facultad_correcta;
        string hora_correcta;
        int pidiendo_cita = -1;

        public MainDialog(ILogger<MainDialog> logger, LuisExRecognizer luisExRecognizer)
            : base(nameof(MainDialog))
        {
            _logger = logger;
            _luisExRecognizer = luisExRecognizer;

            var waterfallSteps = new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                AskDatos,
                RecogeFacultad,
                RecogeCorreo,
                FinalStepAsync
  
            };


            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisExRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "¿En qué te puedo ayudar?\n";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);

            
        }

 private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)

            var luisResult = await _luisExRecognizer.Predict(stepContext.Context.Activity.Text);

            var texto_informacion = "";
            
            switch (luisResult.Prediction.TopIntent)
            {
                case "PedirCita":
                    var la_cita = LuisEx.Convert(luisResult.Prediction);
                    var resultado = LuisEx.ComprobarDatos(la_cita);      
                    switch(resultado){
                        case 0:

                            bool correcto = await LuisEx.PedirCita(la_cita);
                            
                            if(!correcto){
                                texto_informacion = InformationText();
                                var errorCardAttachment3 = new HeroCard(null, null, texto_informacion).ToAttachment();
                                var chatActivity4 = Activity.CreateMessageActivity();
                                chatActivity4.Attachments.Add(errorCardAttachment3);
                                await stepContext.Context.SendActivityAsync(chatActivity4);
                                return await stepContext.ReplaceDialogAsync(InitialDialogId, RepromptMsgHora , cancellationToken);
                            }

                            //var askcorreomessage = MessageFactory.Text("Dime un correo con el que registrar la cita", null, InputHints.IgnoringInput);
                            ciudad_correcta = la_cita.Entities.Cita[0].FullCita[0].Ciudad[0];
                            facultad_correcta = la_cita.Entities.Cita[0].FullCita[0].Facultad[0];
                            hora_correcta = la_cita.Entities.Cita[0].FullCita[0].Hora[0];
                            pidiendo_cita = 0;
                            //await stepContext.Context.SendActivityAsync(askcorreomessage, cancellationToken);

                        break;

                        case 1:
                            // COMBINACION INCORRECTA
                            //Si el dato es incorrecto, se corta el flujo del Waterfall y se vuelve a InitialDialogId con el mensaje de RepromptMsgCiudad
                            texto_informacion = InformationText();
                            var errorCardAttachment = new HeroCard(null, null, texto_informacion).ToAttachment();
                            var chatActivity2 = Activity.CreateMessageActivity();
                            chatActivity2.Attachments.Add(errorCardAttachment);
                            await stepContext.Context.SendActivityAsync(chatActivity2);
                            return await stepContext.ReplaceDialogAsync(InitialDialogId, RepromptMsgNoCom , cancellationToken);

                        case 2:
                            //VALOR INCORECTO
                            texto_informacion = InformationText();
                            var errorCardAttachment2 = new HeroCard(null, null, texto_informacion).ToAttachment();
                            var chatActivity3 = Activity.CreateMessageActivity();
                            chatActivity3.Attachments.Add(errorCardAttachment2);
                            await stepContext.Context.SendActivityAsync(chatActivity3);
                            return await stepContext.ReplaceDialogAsync(InitialDialogId, RepromptError , cancellationToken);

                    }
                    
                    break;

                case "ConsultarCita":
                    pidiendo_cita = 2;

                    var options = new PromptOptions()
                    {
                        Prompt = MessageFactory.Text("¿En qué ciudad realizó la cita?"),
                        RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                        Choices = GetChoicesCiudad(),
                    };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);


                case "CancelarCita":
                    pidiendo_cita = 1;
            
                    var options_can = new PromptOptions()
                    {
                        Prompt = MessageFactory.Text("¿En qué ciudad realizó la cita?"),
                        RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                        Choices = GetChoicesCiudad(),
                    };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), options_can, cancellationToken);

                case "Confirmacion":
                    var confirmationMessage = MessageFactory.Text("¡Pues ya estaría! Gracias por usar nuestros servicios :)", null, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(confirmationMessage, cancellationToken);
                    break;

                case "Saludar":
                    var greetingsMessage = MessageFactory.Text("¡Hola!", null, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(greetingsMessage, cancellationToken);
                    break;

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Lo siento, no te he entendido. Please try asking in a different way (intent was {luisResult.Prediction.TopIntent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            var askcorreomessage = MessageFactory.Text(null,null,null);

            if(pidiendo_cita == 0){
                askcorreomessage = MessageFactory.Text("Dime un correo con el que registrar la cita", null, InputHints.IgnoringInput);
            }else{
                askcorreomessage = MessageFactory.Text("", null, InputHints.IgnoringInput);
            }
            
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = askcorreomessage }, cancellationToken);

        }

        private async Task<DialogTurnResult> AskDatos(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Pidiendo una cita
            if(pidiendo_cita == 0){

                var luisResult = await _luisExRecognizer.Predict(stepContext.Context.Activity.Text);
                var el_correo = LuisEx.Convert(luisResult.Prediction);
                Console.WriteLine(el_correo);


                switch (luisResult.Prediction.TopIntent)
                {
                    case "RecibirCorreo":
                        await LuisEx.RealizarReserva(el_correo,ciudad_correcta,facultad_correcta,hora_correcta);

                        
                        var texto_cita = GetCita(ciudad_correcta, facultad_correcta, hora_correcta);
                        var responseCardAttachment = new HeroCard("Tu cita ha sido asignada con éxito ", null, texto_cita).ToAttachment();
                                            
                        var chatActivity = Activity.CreateMessageActivity();
                        chatActivity.Attachments.Add(responseCardAttachment);
                        await stepContext.Context.SendActivityAsync(chatActivity);


                    break;
                    default:
                        var didntUnderstandMessageText = $"Ha ocurrido un error :(";
                        var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                        await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;

                }   
            }else if(pidiendo_cita == 1 || pidiendo_cita == 2){
                switch (((FoundChoice)stepContext.Result).Value)
                {
                case "granada":
                    ciudad_correcta = "granada";
                    var options = new PromptOptions()
                    {
                        Prompt = MessageFactory.Text("¿En qué facultad realizó la cita?"),
                        RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                        Choices = GetChoicesGranada(),
                    };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
                
                case "ceuta":
                    ciudad_correcta = "ceuta";
                    var options2 = new PromptOptions()
                    {
                        Prompt = MessageFactory.Text("¿En qué facultad realizó la cita?"),
                        RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                        Choices = GetChoicesCeuta(),
                    };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), options2, cancellationToken);
                    
                    
                case "melilla":
                    ciudad_correcta = "melilla";
                    var options3 = new PromptOptions()
                    {
                        Prompt = MessageFactory.Text("¿En qué facultad realizó la cita?"),
                        RetryPrompt = MessageFactory.Text("That was not a valid choice"),
                        Choices = GetChoicesMelilla(),
                    };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), options3, cancellationToken);  
                    
                }
            
            }

            pidiendo_cita = -1;
            return await stepContext.NextAsync(null, cancellationToken);
        }


         private async Task<DialogTurnResult> RecogeFacultad(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // CANCELACION
            if(pidiendo_cita == 1 || pidiendo_cita == 2){
                facultad_correcta = ((FoundChoice)stepContext.Result).Value;
                var askcorreomessage = MessageFactory.Text("Dime el correo con el que reservó la cita", null, InputHints.IgnoringInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = askcorreomessage }, cancellationToken);
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> RecogeCorreo(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // CANCELACION
            if(pidiendo_cita == 1 || pidiendo_cita == 2){
                string respuesta = "";
                string correo = stepContext.Result.ToString();
                correo = correo.ToUpper();
                //Console.WriteLine(correo);

                string id = "";
                ciudad_correcta = ciudad_correcta.ToUpper();
                facultad_correcta = facultad_correcta.ToUpper();

                if(facultad_correcta.Equals("BELLAS ARTES")){
                    id = ciudad_correcta + "ARTES";
                }else{ 
                    id = ciudad_correcta + facultad_correcta;
                }

                id = id.ToLower();

                if(pidiendo_cita == 1){
                    await CosmosBestBot.UpdateItemAsync(id,correo,"LIBRE");   
                    var cancelMessage = MessageFactory.Text("Tu cita ha sido cancelada con éxito", null, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(cancelMessage, cancellationToken);
                }else{
                    respuesta = await CosmosBestBot.GetItemAsync(id,correo);   
                    var consultMessage = MessageFactory.Text(respuesta, null, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(consultMessage, cancellationToken);
                }

            }
            
            return await stepContext.NextAsync(null, cancellationToken);
        } 

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Restart the main dialog with a different message the second time around
            var promptMessage = "¿Qué más puedo hacer por ti?";
            pidiendo_cita = -1;
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }

        public static string InformationText()
        {
            string mensaje = "";

        
            mensaje += "Le recordamos que se debe proporcionar:\n\n";
            mensaje += "***Ciudades Disponibles***: Granada, Ceuta y Melilla\n\n";
            mensaje += "***Facultades Disponibles***\n\n";
            mensaje += ">***Granada***: Informática y Bellas Artes\n\n";
            mensaje += ">***Ceuta***: Arquitectura \n\n";
            mensaje += ">***Melilla***: Salud\n\n";
            mensaje += "***Horario Disponibles***: Citas de 09:00 a 12:30\n\n";

          
            return mensaje;

        }

        public static string GetCita(string ciudad, string facultad, string hora){
            string mensaje = "";

            mensaje += $"**Cita completada:**\n\n";
            mensaje += $">***Facultad***:  {facultad}\n\n";
            mensaje += $">***Ciudad***:  {ciudad}\n\n";
            mensaje += $">***Hora***:  {hora}\n\n";

            return mensaje;

        }

        
        private IList<Choice> GetChoicesCiudad()
        {
            var cardOptions = new List<Choice>()
            {
                new Choice() { Value = "granada"},
                new Choice() { Value = "ceuta"},
                new Choice() { Value = "melilla"},
            };

            return cardOptions;
        }

        
        private IList<Choice> GetChoicesGranada()
        {
            var cardOptions = new List<Choice>()
            {
                new Choice() { Value = "informatica"},
                new Choice() { Value = "bellas artes"},
            };

            return cardOptions;
        }

        
        private IList<Choice> GetChoicesCeuta()
        {
            var cardOptions = new List<Choice>()
            {
                new Choice() { Value = "arquitectura"},

            };

            return cardOptions;
        }

        
        private IList<Choice> GetChoicesMelilla()
        {
            var cardOptions = new List<Choice>()
            {
                new Choice() { Value = "salud"},
            };

            return cardOptions;
        }


   
    }
}