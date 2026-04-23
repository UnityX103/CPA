namespace APP.Pomodoro.Event
{
    public readonly struct E_PlayerCardAdded
    {
        public readonly string PlayerId;
        public E_PlayerCardAdded(string playerId) => PlayerId = playerId;
    }

    public readonly struct E_PlayerCardRemoved
    {
        public readonly string PlayerId;
        public E_PlayerCardRemoved(string playerId) => PlayerId = playerId;
    }
}
