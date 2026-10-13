# Payouts and Island NFT mints since 12 Oct 2026 (V5 M9h, C9h.2)

From `GET /api/admin/report` on the live API after the M9h deploy (since 2026-10-12, read 2026-10-13T18:42 UTC, devnet). Every amount is devnet SOL.
No payout is pending, unconfirmed or failed (`GET /api/admin/treasury`: `payoutsNotSent: []`), so no pre-hotfix `unconfirmed` payout is left to check by hand.

## Totals

| Reason | Status | Payouts | SOL |
|---|---|---|---|
| fund editor test wallet | sent | 1 | 0.5 |
| fund owner Seeker wallet | sent | 1 | 1 |
| market_sale | sent | 4 | 0.08 |
| raid_win | sent | 2 | 0.04 |
| wager_refund | sent | 5 | 0.130999 |
| weekly_reward | sent | 2 | 0.8 |
| **all sent** | | 15 | **2.551** |

Island NFT mints: 8 minted (about 0.018 SOL of rent and fees each from the treasury).

Deposits into the treasury: market_buy consumed 4 (0.08 SOL); raid_wager consumed 2 (0.02 SOL); wager consumed 5 (0.09 SOL); wager refunded 5 (0.130999 SOL).

## Payouts

| Id | Created (UTC) | Wallet | SOL | Reason | Ref | Status | Transaction |
|---|---|---|---|---|---|---|---|
| 1 | 2026-10-12T02:11 | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | 0.5 | fund editor test wallet |  | sent | [4HwD1GYu...](https://explorer.solana.com/tx/4HwD1GYuKNs4jcfZLTsnActh8S8D6ZLxHgvsNJufKw3jP1R4bkp9bhRSkpocZA4eYBHa3veiYswBm5xbs7WB3N7h?cluster=devnet) |
| 2 | 2026-10-12T02:12 | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | 0.02 | raid_win | 68689272-37e0-481d-9cb7-a0f8c2cd9593 | sent | [3DSSn4Ks...](https://explorer.solana.com/tx/3DSSn4KsCA7GTa5yQRA6VcbCFWon1p4i8ieqPMcCpnbTxaoGBXMkZ3JyKcLREtQ8GDmjiQDHexfZEwr9C81rB8cw?cluster=devnet) |
| 3 | 2026-10-12T02:13 | `3cm6nYP9gUmSuQ2GyWSNSNXwUvEBKKFUGEhyXSp9irGB` | 0.02 | market_sale | 29144204-e89d-4d17-b9d5-41db3606f271 | sent | [5HZjSaNx...](https://explorer.solana.com/tx/5HZjSaNx5XYsCob3sRsn3A9mbqz9F6MfUGsaHJu1oJdwRkVdTajRoJnEvyhuymiqcKgRcZ46wuXqB6D56RPwUKta?cluster=devnet) |
| 4 | 2026-10-12T02:13 | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | 0.01 | wager_refund | dep:3 | sent | [5xKXvS4e...](https://explorer.solana.com/tx/5xKXvS4eBUREKBiKUGioywjMbwtw4CP3pPDzYtDFbCBADrhst9b1r44GJYHMEyye9LxQQCD8h95CnzxRpN4JordM?cluster=devnet) |
| 5 | 2026-10-12T02:13 | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | 0.5 | weekly_reward | wr:1 | sent | [4D8ZyhNE...](https://explorer.solana.com/tx/4D8ZyhNELm8ws6H54iqEgGy8W8Fyo673162wQ8RZj6PmxuUSJvuDharG5BxFAw6d4tQmHsfZnkddWbBRfBgufgsZ?cluster=devnet) |
| 6 | 2026-10-12T02:13 | `3cm6nYP9gUmSuQ2GyWSNSNXwUvEBKKFUGEhyXSp9irGB` | 0.3 | weekly_reward | wr:2 | sent | [4jAroDRc...](https://explorer.solana.com/tx/4jAroDRctD3g2JvpXxwaR8Qznv7D3mLrAznagQaJnbx5HGTV6DdmMpRMHpf6fj76RJGhBqrAaHEsXyz5yjrqbZ5s?cluster=devnet) |
| 7 | 2026-10-12T02:33 | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | 0.01 | wager_refund | dep:5 | sent | [4UGrfADC...](https://explorer.solana.com/tx/4UGrfADC3DZJ2PihwdusoQ9ivy8jGM3TGN7ouJ5kwSdJtY2jrM1vkQ6Y3cCuPT9S2af58Ef5kQW6kRDdue9WiD9M?cluster=devnet) |
| 8 | 2026-10-12T03:13 | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | 0.02 | raid_win | e443c63a-3f7e-4f21-91c1-27055552b3ca | sent | [3KUSwXX7...](https://explorer.solana.com/tx/3KUSwXX7BaEcnCUqjCdxH3cTxL9s7Ez8mrvATi6Gq33UcEYsYVZWR7WXCVUkqFXvC2neqpzUVEvC6sZFU8Xtk2Nx?cluster=devnet) |
| 9 | 2026-10-12T03:16 | `5eUkYhzqte5YoNM4fLSVMLNBtQpARckMwX7wnMftGqBF` | 0.005 | market_sale | 5720ea7b-7b0a-41b3-ad42-07b5c4859547 | sent | [TynLgz1v...](https://explorer.solana.com/tx/TynLgz1vTxmjiXMh6VxPnE2KfjpKHVUwpRyC3mGfCuAnz12DM9YrXcTPCFihFQkPpoG9f1TwEq3BsbPpgQiNPeG?cluster=devnet) |
| 10 | 2026-10-12T03:21 | `EKczu9LC91ePZNjHu6tnnqyAX3o6dEbeScY9wkMbdTYK` | 0.005 | market_sale | 737d2ea9-a561-4eaa-b7e4-67fc9ccbd147 | sent | [4xyaFvBc...](https://explorer.solana.com/tx/4xyaFvBcyH3uDiMHJfN7uLRwCzQDjspDBFa1aA9ZmTqHD44q8xnR6RmRuqtGCcHTAHWTKoSsewq4AA5A2oPH8WEe?cluster=devnet) |
| 11 | 2026-10-12T06:28 | `wd8iCRH9yMYrBec78xfY53bttt1Vhxwk24nvWb539ak` | 1 | fund owner Seeker wallet |  | sent | [vaZSZ4nU...](https://explorer.solana.com/tx/vaZSZ4nUYLh5UJJA7y7mZBGCWwGB92TUdDX6dLhxmHH4o4TezGkusvcLntpxoLdRuaZsbTVEoBW14RMYAH9PkdW?cluster=devnet) |
| 12 | 2026-10-12T18:04 | `8cpFX8XzRJMGjdtR48wy3Kc4S8zf88YgdJ4X1b9LJFTn` | 0.1 | wager_refund | dep:10 | sent | [2EcSnGGD...](https://explorer.solana.com/tx/2EcSnGGDScuSNmLGbQBv6vK4KPRYrzfFUb7mFSPNV2YDrF22xPiSbfKvtriTqec4CLuXQdyY1q2e7yv4gkqQejWT?cluster=devnet) |
| 13 | 2026-10-13T06:10 | `wd8iCRH9yMYrBec78xfY53bttt1Vhxwk24nvWb539ak` | 0.01 | wager_refund | dep:11 | sent | [59JSMiuN...](https://explorer.solana.com/tx/59JSMiuNCozHPCgM7trBHRktH8JDX6t48JpjZca2KhzEw6bHsKSb2pHetCMSUGZ9TXmZQJywv1VVVtjoAwke2Bde?cluster=devnet) |
| 14 | 2026-10-13T07:47 | `8cpFX8XzRJMGjdtR48wy3Kc4S8zf88YgdJ4X1b9LJFTn` | 0.05 | market_sale | c885cf73-249d-4c9d-bf0d-8dd4560ba4bf | sent | [2shrpNF6...](https://explorer.solana.com/tx/2shrpNF6NxTuRBJG2BbVuewDyfsKmENJUAqj8h3QsgJsXRsViqrxZNLen3s2zBo1MLWtoxAonTvqkem6E8wZdXhT?cluster=devnet) |
| 15 | 2026-10-13T18:41 | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | 0.000999 | wager_refund | dep:16 | sent | [29ATXujD...](https://explorer.solana.com/tx/29ATXujDEL2fNsUYuE2BrV6KGr49ejz473ewJ1Hi1SGeQAEkuPn5t92JPUY97akByiF3zLxyMgBUUf6wDKvZ8wWf?cluster=devnet) |

## Island NFT mints

| Island | Owner wallet | Status | Mint | Sent (UTC) | Transaction |
|---|---|---|---|---|---|
| Smoke Cove A | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | minted | `6fHK7oRnziLtqEK9FDRjh1prQ1B6o7uRMUbpkUhaYtAu` | 2026-10-12T02:12 | [4fzCFBHy...](https://explorer.solana.com/tx/4fzCFBHygX3L8SCubwg5DbNDP93yqqpi6CJ2trwGJ3aNoTT2FzxoENCtShTqCAbbvszMHG5eeAyDwAjBnads8r8J?cluster=devnet) |
| Smoke Cove B | `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr` | minted | `E7ZGHiesWXHUhVguoWydq88VnbuTzk9FhnMjcXcmdqMg` | 2026-10-12T02:12 | [2MXtsi6s...](https://explorer.solana.com/tx/2MXtsi6s4eVCfwi2RcUAXLGJYimsRS7z8b6vtmxw3tGixsHBgJFtsdFgmrvfkV4BgwCHkFV6XpKCCi7fYvsrJ6eu?cluster=devnet) |
| Rum Haven | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | minted | `FCpNXyvtGvV231qef91BGWSeuyoMUh1q6EQwYhvd6ZNm` | 2026-10-12T02:57 | [3NNZUB5u...](https://explorer.solana.com/tx/3NNZUB5uC2ziz8GPhv7hYHfMdJ8z44PJbTSUWPdaZopHff6xUNU6YsSMMauZT5PoZMiNnRnR6jvH6UZ6SLdgFteK?cluster=devnet) |
| Kraken Keep | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | minted | `EewggiTXBgYRrZ3VGYLqPqY4qm3VYxtRjNhPEf5y7u7v` | 2026-10-12T03:15 | [4XRmtq1c...](https://explorer.solana.com/tx/4XRmtq1cfNhQygk3nuf73tfre7XnGQ7CkTCq3LkggF6XWN7SS9aaEnci4uXSyd6KuJmbqdX6SuZNVsARaPPDKyuD?cluster=devnet) |
| Siren Shoal | `5SBguAPTSK7ud2iY2spSDmtBe6CYL2u7vQyi5YaNjH9A` | minted | `A9vrJxGNctLdqa7NqpXjPdQoNaZ7BfWvYfDsfJtyQYP2` | 2026-10-12T03:20 | [4SJt2Ypu...](https://explorer.solana.com/tx/4SJt2YpuvrYDyCmyAGfMQskErMh8dLpn7j8uL7THrkyv1XsfE4Uwvy9eTzFkEpiBhBXt4Z5FPAtcvTuYissLjkJW?cluster=devnet) |
| sjsjsnzn | `wd8iCRH9yMYrBec78xfY53bttt1Vhxwk24nvWb539ak` | minted | `vMCPFZb1ERxEhKLq5Y59sGjZiGj6qonFt8JkChAXq7t` | 2026-10-12T06:49 | [5QDcs8M9...](https://explorer.solana.com/tx/5QDcs8M9rjEDNtUvSgdb59Q3h8xrBV2P88nSq5Nk7ArVmqc1CX7V2S1ZjSZeZ1EtGHFchre6MReH8sPwwb2eLHQq?cluster=devnet) |
| as12 | `wd8iCRH9yMYrBec78xfY53bttt1Vhxwk24nvWb539ak` | minted | `5UNgeg6gDXhnWeJ6p3vkQNPNgYsKYHi42iahjd8LmcMR` | 2026-10-12T11:38 | [3MusMGZ7...](https://explorer.solana.com/tx/3MusMGZ7HoVrzyRVS5mBW186maLpPwHj3NTyhEgmKargaXCNPBCfRk5R91ZiBQKVdCbuH13Dm2GEPdnhwMbmYUdw?cluster=devnet) |
| whirlpool | `3szwDJBGiQj4hDjXuxudYSMsZ4Zb7WKbyXe9tmxxFkFn` | minted | `35HdU19wDSQU7QGMkgUMKcVgWofvoWzCbcJeoeuqwsaf` | 2026-10-12T19:16 | [2YaL84Us...](https://explorer.solana.com/tx/2YaL84UsfV9q4ygyyxYqp2E4o76gaNsAG4dM3WNbZKHzwzS7MBeRrUHVXAzyhXSWo4V4fsJkGYr2VwoJWGGGeBwF?cluster=devnet) |

Mint times of islands minted before M9h are their claim times (migration AddMintSafety).
