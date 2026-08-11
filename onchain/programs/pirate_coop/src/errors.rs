use anchor_lang::prelude::*;

#[error_code]
pub enum PirateCoopError {
    #[msg("Match is full")]
    MatchFull,
    #[msg("Match not in lobby")]
    NotInLobby,
    #[msg("Match not running")]
    NotRunning,
    #[msg("Not a player in this match")]
    NotPlayer,
    #[msg("Invalid capacity")]
    InvalidCapacity,
    #[msg("Tick phase failed")]
    TickFailed,
    #[msg("Config data too large")]
    ConfigTooLarge,
}
