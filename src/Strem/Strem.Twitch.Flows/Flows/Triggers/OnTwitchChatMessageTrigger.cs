﻿using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Strem.Core.Events.Bus;
using Strem.Core.Extensions;
using Strem.Core.Flows;
using Strem.Core.Flows.Processors;
using Strem.Core.Flows.Triggers;
using Strem.Core.State;
using Strem.Core.Types;
using Strem.Core.Variables;
using Strem.Twitch.Extensions;
using Strem.Twitch.Flows.Flows.Triggers.Data;
using Strem.Twitch.Services.Client;
using Strem.Twitch.Types;
using Strem.Twitch.Variables;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Models;
using UserType = TwitchLib.Api.Core.Enums.UserType;

namespace Strem.Twitch.Flows.Flows.Triggers;

public class OnTwitchChatMessageTrigger : FlowTrigger<OnTwitchChatMessageTriggerData>
{
    public override string Code => OnTwitchChatMessageTriggerData.TriggerCode;
    public override string Version => OnTwitchChatMessageTriggerData.TriggerVersion;

    public static VariableEntry ChatMessageVariable = new("chat.message", TwitchVars.TwitchContext);
    public static VariableEntry BitsSentVariable = new("chat.message.bits-sent", TwitchVars.TwitchContext);
    public static VariableEntry BitsValueVariable = new("chat.message.bits-value", TwitchVars.TwitchContext);
    public static VariableEntry RewardIdVariable = new("chat.message.reward-id", TwitchVars.TwitchContext);
    public static VariableEntry IsNoisyVariable = new("chat.message.is-noisy", TwitchVars.TwitchContext);
    public static VariableEntry SubscriptionLengthVariable = new("chat.message.subscription-length", TwitchVars.TwitchContext);
    public static VariableEntry IsHighlightedVariable = new("chat.message.is-highlighted", TwitchVars.TwitchContext);
    public static VariableEntry UserTypeVariable = new("chat.user-type", TwitchVars.TwitchContext);
    
    public override string Name => "On Twitch Chat Message";
    public override string Description => "Triggers when a twitch chat message is received";

    public override VariableDescriptor[] VariableOutputs { get; } = new[]
    {
        ChatMessageVariable.ToDescriptor(), BitsSentVariable.ToDescriptor(), BitsValueVariable.ToDescriptor(),
        RewardIdVariable.ToDescriptor(), IsNoisyVariable.ToDescriptor(), SubscriptionLengthVariable.ToDescriptor(),
        IsHighlightedVariable.ToDescriptor(), UserTypeVariable.ToDescriptor()
    };

    public IObservableTwitchClient TwitchClient { get; set; }
    
    public OnTwitchChatMessageTrigger(ILogger<FlowTrigger<OnTwitchChatMessageTriggerData>> logger, IFlowStringProcessor flowStringProcessor, IAppState appState, IEventBus eventBus, IObservableTwitchClient twitchClient) : base(logger, flowStringProcessor, appState, eventBus)
    {
        TwitchClient = twitchClient;
    }

    public override bool CanExecute() => AppState.HasTwitchScope(ChatScopes.ReadChat);

    public IVariables PopulateVariables(ChatMessage chatMessage)
    {
        var flowVars = new Core.Variables.Variables();
        flowVars.Set(ChatMessageVariable, chatMessage.Message);
        flowVars.Set(BitsSentVariable, chatMessage.Bits.ToString());
        flowVars.Set(BitsValueVariable, chatMessage.BitsInDollars.ToString());
        flowVars.Set(RewardIdVariable, chatMessage.CustomRewardId);
        flowVars.Set(IsNoisyVariable, (chatMessage.Noisy == Noisy.True).ToString());
        flowVars.Set(SubscriptionLengthVariable, chatMessage.SubscribedMonthCount.ToString());
        flowVars.Set(IsHighlightedVariable, chatMessage.IsHighlighted.ToString());
        flowVars.Set(UserTypeVariable, chatMessage.UserType.ToString());
        return flowVars;
    }

    public bool DoesMessageMeetCriteria(OnTwitchChatMessageTriggerData data, ChatMessage chatMessage)
    {
        if (data.MinimumUserType == UserType.Broadcaster && !chatMessage.IsBroadcaster) { return false; }
        
        var isSomeFormOfAdminRole = data.MinimumUserType is UserType.Staff or UserType.GlobalModerator or UserType.Admin;
        if (isSomeFormOfAdminRole && !chatMessage.IsStaff) { return false; }
        if (data.MinimumUserType == UserType.Moderator && !chatMessage.IsModerator) { return false; }
        if (data.MinimumUserType == UserType.VIP && !chatMessage.IsVip) { return false; }
        
        if(data.IsSubscriber && !chatMessage.IsSubscriber) { return false; }
        if(data.HasBits && chatMessage.Bits <= 0) { return false; }
        if(data.HasChannelReward && string.IsNullOrEmpty(chatMessage.CustomRewardId)) { return false; }

        if (data.MatchType != TextMatch.None)
        {
            if (!chatMessage.Message.MatchesText(data.MatchType, data.MatchText))
            { return false; }
        }

        return true;
    }

    public override IObservable<IVariables> Execute(OnTwitchChatMessageTriggerData data)
    {
        return TwitchClient.OnMessageReceived
            .Where(x => DoesMessageMeetCriteria(data, x.ChatMessage))
            .Select(x => PopulateVariables(x.ChatMessage));
    }
}