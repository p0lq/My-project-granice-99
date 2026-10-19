// Все звуки игры. Файлы лежат в Assets/Resources/Audio/<Имя>/: каждый файл в папке
// это вариант звука, при проигрывании выбирается случайный. Нет папки — звук молчит.
// Сейчас там наборы Kenney (CC0); любой можно заменить, положив свои файлы в папку.
public enum SoundId
{
    Footstep,
    FootstepHard,
    PlayerAttack,
    Hit,
    Gunshot,
    PlayerHurt,
    ZombieBreath,
    ZombieScream,
    ZombieAttack,
    ZombieDeath,
    Pickup,
    DoorOpen,
    DoorClose,
    MetalDoor,
    DoorBash,
    Locked,
    Crate,
    Well,
    Lever,
    Click,
    UIClick,
    Win,
    Lose,
    AmbientWind,
    AmbientHum,
}
