using System.Collections;
using Microsoft.Xna.Framework;
using Celeste.Mod.Entities;
using Monocle;

namespace Celeste.Mod.Madhunt {
    [CustomEntity("Madhunt/C3OshiroSecret")]
    public class C3OshiroSecret : NPC {
        private Player player;
        private BadelineDummy badeline;

        public C3OshiroSecret(EntityData data, Vector2 off) : base(data.Position + off) {
            Add(Sprite = new OshiroSprite((int) Facings.Left));
            Add(Light = new VertexLight(-Vector2.UnitY * 16f, Color.White, 1f, 32, 64));
            Add(Talker = new TalkComponent(new Rectangle(-16, -8, 32, 8), new Vector2(0f, -24f), OnTalk, null));
            MoveAnim = "move";
            IdleAnim = "idle";
        }

        private void OnTalk(Player player) {
            Level.StartCutscene(EndTalking);
            Add(new Coroutine(Talk(player)));
        }

        private IEnumerator Talk(Player player) {
            this.player = player;
            yield return PlayerApproach(player, false, 12);
            yield return Textbox.Say("Madhunt_03_Oshiro_Secret", BadelineAppear, BadelineVanishes);
            yield return PlayerLeave(player);
            EndTalking(SceneAs<Level>());
        }

        private void EndTalking(Level level) {
            Player player = Scene.Entities.FindFirst<Player>();
            if(player != null) {
                player.StateMachine.Locked = false;
                player.StateMachine.State = Player.StNormal;
            }
            badeline?.RemoveSelf();
            Talker.Enabled = false;
        }

        private IEnumerator BadelineAppear() {
            Audio.Play("event:/char/badeline/maddy_split", player.Position);
            Level.Add(badeline = new BadelineDummy(player.Center));
            Level.Displacement.AddBurst(badeline.Center, 0.5f, 8f, 32f, 0.5f);
            badeline.Sprite.Scale.X = (int) Facings.Right;
            yield return badeline.FloatTo(player.Center + new Vector2(-18f, -10f), (int) Facings.Right, false);
            yield return 0.2f;
        }

        private IEnumerator BadelineVanishes() {
            yield return 0.2f;
            badeline.Vanish();
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            badeline = null;
            yield return 0.2f;
        }
    }
}