const SteamTotp = require('steam-totp');
const getCode = () => SteamTotp.generateAuthCode(process.env.STEAM_SHARED_SECRET);

const initialCode = getCode();
let currentCode = initialCode;
do {
    currentCode = getCode();
} while (currentCode === initialCode);

console.log(currentCode);