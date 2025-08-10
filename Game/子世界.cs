using Engine;
using Engine.Graphics;
using GameEntitySystem;
using System.Collections.Generic;
using TemplatesDatabase;

namespace Game
{
    /*
     * 添加一个世界的方法：
     * 1.在WorldType枚举类添加一个类型值，如Test
     * 2.在WorldParameter类的Collections数组添加一行相似元素，如 new string[4] { "Test", "测试世界", "26", "0,255,128,128" } ，Collections数组长度也要改（new string[5][]）
     * 3.为新添加的世界类型写对应的地形构造类，如果不写默认原版地形
     * 4.就行了（去除世界是一样的，逆过来）
     * 5.如果新世界要引用外部存档，则把存档塞scmod里，存档名称和世界类型名一样即可，如Exist.scworld（超平坦存档要换超平坦对应的地形构造器，其他同理）
     */

    public enum WorldType
    {
        //Default对应主世界，无特别需要不修改
        Default, Ashes, Desert, Snowfield, Limit, Exist
    }

    public static class WorldParameter
    {
        //世界类型名，中文标识名，世界传送门方块ID，世界传送门贴图颜色
        public static string[][] Collections = new string[5][]
        {
            new string[4] { "Ashes", "灰烬世界", "67", "255,0,255,128" },
            new string[4] { "Desert", "沙漠世界", "4", "255,255,0,128" },
            new string[4] { "Snowfield", "冰雪世界", "62", "0,255,255,128" },
            new string[4] { "Limit", "限制世界", "2", "255,128,128,128" },
            new string[4] { "Exist", "现有世界", "46", "128,255,128,128" }
        };
    }

    public class SubsystemWorldDemo : SubsystemWorld, IUpdateable
    {
        public SubsystemTerrain subsystemTerrain;

        public SubsystemWeather subsystemWeather;

        public WorldType worldType;

        public new void Update(float dt)
        {
            base.Update(dt);
            if (worldType == WorldType.Default)
            {
                //如果为主世界，则怎么样
            }
            else if (worldType == WorldType.Snowfield)
            {
                //如果为冰雪世界，常年下雪
                subsystemWeather.m_precipitationStartTime = 0f;
            }
            else if(worldType == WorldType.Limit)
            {
                //如果为限制世界，玩家属性最优，可以多段跳跃
                if (base.m_componentPlayer != null)
                {
                    base.m_componentPlayer.ComponentFlu.m_fluDuration = 0f;
                    base.m_componentPlayer.ComponentFlu.m_coughDuration = 0f;
                    base.m_componentPlayer.ComponentSickness.m_sicknessDuration = 0f;
                    base.m_componentPlayer.ComponentSickness.m_greenoutDuration = 0f;
                    base.m_componentPlayer.ComponentVitalStats.Sleep = 1f;
                    base.m_componentPlayer.ComponentVitalStats.Stamina = 1f;
                    base.m_componentPlayer.ComponentVitalStats.Temperature = 12f;
                    base.m_componentPlayer.ComponentVitalStats.Wetness = 8f;
                    //多段跳跃
                    if (base.m_componentPlayer.ComponentInput.PlayerInput.Jump && base.m_componentPlayer.ComponentLocomotion.m_falling)
                    {
                        Vector3 velocity = base.m_componentPlayer.ComponentBody.Velocity;
                        base.m_componentPlayer.ComponentBody.Velocity = new Vector3(velocity.X, 7.5f, velocity.Z);
                    }
                }
            }
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            subsystemTerrain = base.m_subsystemTerrain;
            subsystemWeather = base.Project.FindSubsystem<SubsystemWeather>(true);
            worldType = base.m_worldType;
            //如果为子世界，则更改地形构造
            switch (worldType)
            {
                case WorldType.Ashes: subsystemTerrain.TerrainContentsGenerator = new AshesTerrainGenerator(subsystemTerrain); break;
                case WorldType.Desert: subsystemTerrain.TerrainContentsGenerator = new DesertTerrainGenerator(subsystemTerrain); break;
                case WorldType.Snowfield: subsystemTerrain.TerrainContentsGenerator = new SnowfieldTerrainGenerator(subsystemTerrain); break;
                case WorldType.Limit: subsystemTerrain.TerrainContentsGenerator = new LimitTerrainGenerator(subsystemTerrain); break;
                case WorldType.Exist: subsystemTerrain.TerrainContentsGenerator = new TerrainContentsGeneratorFlat(subsystemTerrain); break;
                default: break;
            }
            //如果为灰烬世界，则修改材质包，时间固定为黄昏，天气效果关闭
            if (worldType == WorldType.Ashes)
            {
                base.Project.FindSubsystem<SubsystemBlocksTexture>(true).BlocksTexture = ContentManager.Get<Texture2D>("寂静岭材质包");
                base.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.TimeOfDayMode = TimeOfDayMode.Sunset;
                base.Project.FindSubsystem<SubsystemGameInfo>(true).WorldSettings.AreWeatherEffectsEnabled = false;
            }
            //如果为冰雪世界，则更改动物生成
            else if (worldType == WorldType.Snowfield)
            {
                ChangeCreatureTypes();
            }
        }

        public override void OnEntityAdded(Entity entity)
        {
            //如果为灰烬世界，则更改动物的部分属性
            if (worldType == WorldType.Ashes)
            {
                ComponentCreature componentCreature = entity.FindComponent<ComponentCreature>();
                ComponentPlayer componentPlayer = entity.FindComponent<ComponentPlayer>();
                if (componentCreature != null && componentPlayer == null)
                {
                    componentCreature.ComponentCreatureModel.TextureOverride = ContentManager.Get<Texture2D>("Textures/Creatures/Jaguar");
                    componentCreature.ComponentLocomotion.FlySpeed = componentCreature.ComponentLocomotion.WalkSpeed * 3;
                    componentCreature.ComponentLocomotion.WalkSpeed = componentCreature.ComponentLocomotion.WalkSpeed * 2f;
                    componentCreature.ComponentHealth.Attacked += delegate
                    {
                        Random random = new Random();
                        if (random.Float(0, 1f) <= 0.04f)
                        {
                            Vector3 creaturePosition = componentCreature.ComponentBody.Position;
                            base.Project.FindSubsystem<SubsystemPickables>().AddPickable(111, 1, creaturePosition, null, null);
                        }
                    };
                }
            }
            base.OnEntityAdded(entity);
        }

        //更改自然生成的动物
        public void ChangeCreatureTypes()
        {
            SubsystemCreatureSpawn subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
            subsystemCreatureSpawn.m_creatureTypes.Clear();
            subsystemCreatureSpawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Seagull", SpawnLocationType.Surface, randomSpawn: true, constantSpawn: false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
                {
                    float oceanShoreDistance = m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                    return (oceanShoreDistance < 8f) ? 5f : 0f;
                },
                SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => subsystemCreatureSpawn.SpawnCreatures(creatureType, "Seagull", point, 1).Count)
            });
            subsystemCreatureSpawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("White Tigers", SpawnLocationType.Surface, randomSpawn: true, constantSpawn: false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
                {
                    float oceanShoreDistance = m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                    return (oceanShoreDistance > 8f) ? 5f : 0f;
                },
                SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => subsystemCreatureSpawn.SpawnCreatures(creatureType, "Tiger_White", point, 1).Count)
            });
            subsystemCreatureSpawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("White Bull", SpawnLocationType.Surface, randomSpawn: true, constantSpawn: false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
                {
                    float oceanShoreDistance = m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                    return (oceanShoreDistance > 8f) ? 5f : 0f;
                },
                SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => subsystemCreatureSpawn.SpawnCreatures(creatureType, "Bull_White", point, 1).Count)
            });
            subsystemCreatureSpawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("Polar Bears", SpawnLocationType.Surface, randomSpawn: true, constantSpawn: false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
                {
                    float oceanShoreDistance = m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                    return (oceanShoreDistance > 8f) ? 5f : 0f;
                },
                SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => subsystemCreatureSpawn.SpawnCreatures(creatureType, "Bear_Polar", point, 1).Count)
            });
            subsystemCreatureSpawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType("White Horse", SpawnLocationType.Surface, randomSpawn: true, constantSpawn: false)
            {
                SpawnSuitabilityFunction = delegate (SubsystemCreatureSpawn.CreatureType creatureType, Point3 point)
                {
                    float oceanShoreDistance = m_subsystemTerrain.TerrainContentsGenerator.CalculateOceanShoreDistance(point.X, point.Z);
                    return (oceanShoreDistance > 8f) ? 5f : 0f;
                },
                SpawnFunction = ((SubsystemCreatureSpawn.CreatureType creatureType, Point3 point) => subsystemCreatureSpawn.SpawnCreatures(creatureType, "Horse_White", point, 1).Count)
            });
        }
    }

    public class SubsystemEntityBlockBehavior : SubsystemBlockBehavior
    {
        public override int[] HandledBlocks => new int[4] { 45, 64, 27, 216 };

        public SubsystemBlockEntities SubsystemBlockEntities;

        public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
        {
            Point3 point3 = new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
            ComponentBlockEntity blockEntity = SubsystemBlockEntities.GetBlockEntity(point3.X, point3.Y, point3.Z);
            //交互时，如果箱子、熔炉、工作台和发射器的方块实体为空，则创建方块实体并添加指定物品
            if (blockEntity == null && componentMiner.ComponentPlayer != null)
            {
                int id = base.Project.FindSubsystem<SubsystemTerrain>().Terrain.GetCellContents(point3.X, point3.Y, point3.Z);
                switch (id)
                {
                    case 45:
                        {
                            DatabaseObject databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("Chest", base.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
                            ValuesDictionary valuesDictionary = new ValuesDictionary();
                            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
                            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point3);
                            Entity entity = base.Project.CreateEntity(valuesDictionary);
                            base.Project.AddEntity(entity);
                            ComponentChest componentChest = entity.FindComponent<ComponentChest>(throwOnError: true);
                            for (int i = 0; i < 15; i++)
                            {
                                componentChest.m_slots[i].Value = new Random().Int(1, BlocksManager.Blocks.Length - 1);
                                componentChest.m_slots[i].Count = 1;
                            }
                            componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new ChestWidget(componentMiner.Inventory, componentChest);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            return true;
                        }
                    case 64:
                        {
                            DatabaseObject databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("Furnace", base.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
                            ValuesDictionary valuesDictionary = new ValuesDictionary();
                            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
                            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point3);
                            Entity entity = base.Project.CreateEntity(valuesDictionary);
                            base.Project.AddEntity(entity);
                            ComponentFurnace componentFurnace = entity.FindComponent<ComponentFurnace>(throwOnError: true);
                            int[] items = new int[3] { 77, 88, 176 };
                            if (new Random().Float(0, 1f) < 0.4f)
                            {
                                componentFurnace.m_slots[0].Value = items[new Random().Int(0, items.Length - 1)];
                                componentFurnace.m_slots[0].Count = 1;
                            }
                            componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new FurnaceWidget(componentMiner.Inventory, componentFurnace);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            return true;
                        }
                    case 27:
                        {
                            DatabaseObject databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("CraftingTable", base.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
                            ValuesDictionary valuesDictionary = new ValuesDictionary();
                            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
                            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point3);
                            Entity entity = base.Project.CreateEntity(valuesDictionary);
                            base.Project.AddEntity(entity);
                            ComponentCraftingTable componentCraftingTable = entity.FindComponent<ComponentCraftingTable>(throwOnError: true);
                            int[] items = new int[15] { 29, 165, 37, 222, 36, 38, 218, 219, 171, 169, 90, 117, 121, 120, 230 };
                            for (int i = 0; i < 9; i++)
                            {
                                if (new Random().Float(0, 1f) < 0.1f)
                                {
                                    componentCraftingTable.m_slots[i].Value = items[new Random().Int(0, items.Length - 1)];
                                    componentCraftingTable.m_slots[i].Count = 1;
                                }
                            }
                            componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new CraftingTableWidget(componentMiner.Inventory, componentCraftingTable);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            return true;
                        }
                    case 216:
                        {
                            DatabaseObject databaseObject = base.Project.GameDatabase.Database.FindDatabaseObject("Dispenser", base.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
                            ValuesDictionary valuesDictionary = new ValuesDictionary();
                            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
                            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point3);
                            Entity entity = base.Project.CreateEntity(valuesDictionary);
                            base.Project.AddEntity(entity);
                            ComponentDispenser componentDispenser = entity.FindComponent<ComponentDispenser>(throwOnError: true);
                            componentMiner.ComponentPlayer.ComponentGui.ModalPanelWidget = new DispenserWidget(componentMiner.Inventory, componentDispenser);
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            return true;
                        }
                }
            }
            return false;
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            SubsystemBlockEntities = base.Project.FindSubsystem<SubsystemBlockEntities>(true);
            SubsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
        }
    }

    public class AirWallBlock : AlphaTestCubeBlock
    {
        public const int Index = 1023;

        //空气墙方块
        public AirWallBlock()
        {
            DefaultCreativeData = -1;
        }

        public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue)
        {
            return false;
        }

        public override float GetDigResilience(int value)
        {
            return float.PositiveInfinity;
        }

        public override float GetProjectileResilience(int value)
        { 
            return float.PositiveInfinity;
        }

        public override float GetExplosionResilience(int value)
        {
            return float.PositiveInfinity;
        }
    }

    //灰烬世界构造
    public class AshesTerrainGenerator : TerrainContentsGenerator22, ITerrainContentsGenerator
    {
        public SubsystemTerrain subsystemTerrain;

        public AshesTerrainGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
        {
            this.subsystemTerrain = subsystemTerrain;
        }

        public new void GenerateChunkContentsPass1(TerrainChunk chunk)
        {
            GenerateSurfaceParameters(chunk, 0, 0, 16, 8);
            NGenerateTerrain(chunk, 0, 0, 16, 8);
        }

        public new void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
            GenerateSurfaceParameters(chunk, 0, 8, 16, 16);
            NGenerateTerrain(chunk, 0, 8, 16, 16);
        }

        public new void GenerateChunkContentsPass3(TerrainChunk chunk)
        {
            GenerateCaves(chunk);
            GeneratePockets(chunk);
            GenerateMinerals(chunk);
            PropagateFluidsDownwards(chunk);
        }

        public new void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            GenerateBedrockAndAir(chunk);
        }

        public void NGenerateTerrain(TerrainChunk chunk, int x1, int z1, int x2, int z2)
        {
            int num = x2 - x1;
            int num2 = z2 - z1;
            _ = m_subsystemTerrain.Terrain;
            int num3 = chunk.Origin.X + x1;
            int num4 = chunk.Origin.Y + z1;
            Grid2d grid2d = new Grid2d(num, num2);
            Grid2d grid2d2 = new Grid2d(num, num2);
            for (int i = 0; i < num2; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    grid2d.Set(j, i, CalculateOceanShoreDistance(j + num3, i + num4));
                    grid2d2.Set(j, i, CalculateMountainRangeFactor(j + num3, i + num4));
                }
            }
            Grid3d grid3d = new Grid3d(num / 4 + 1, 33, num2 / 4 + 1);
            for (int k = 0; k < grid3d.SizeX; k++)
            {
                for (int l = 0; l < grid3d.SizeZ; l++)
                {
                    int num5 = k * 4 + num3;
                    int num6 = l * 4 + num4;
                    float num7 = CalculateHeight(num5, num6);
                    float v = CalculateMountainRangeFactor(num5, num6);
                    float num8 = MathUtils.Lerp(TGMinTurbulence, 1f, Squish(v, TGTurbulenceZero, 1f));
                    for (int m = 0; m < grid3d.SizeY; m++)
                    {
                        int num9 = m * 8;
                        float num10 = TGTurbulenceStrength * num8 * MathUtils.Saturate(num7 - (float)num9) * (2f * SimplexNoise.OctavedNoise(num5, num9, num6, TGTurbulenceFreq, TGTurbulenceOctaves, 4f, TGTurbulencePersistence) - 1f);
                        float num11 = (float)num9 + num10;
                        float num12 = num7 - num11;
                        num12 += MathUtils.Max(4f * (TGDensityBias - (float)num9), 0f);
                        grid3d.Set(k, m, l, num12);
                    }
                }
            }
            int oceanLevel = OceanLevel;
            for (int n = 0; n < grid3d.SizeX - 1; n++)
            {
                for (int num13 = 0; num13 < grid3d.SizeZ - 1; num13++)
                {
                    for (int num14 = 0; num14 < grid3d.SizeY - 1; num14++)
                    {
                        grid3d.Get8(n, num14, num13, out float v2, out float v3, out float v4, out float v5, out float v6, out float v7, out float v8, out float v9);
                        float num15 = (v3 - v2) / 4f;
                        float num16 = (v5 - v4) / 4f;
                        float num17 = (v7 - v6) / 4f;
                        float num18 = (v9 - v8) / 4f;
                        float num19 = v2;
                        float num20 = v4;
                        float num21 = v6;
                        float num22 = v8;
                        for (int num23 = 0; num23 < 4; num23++)
                        {
                            float num24 = (num21 - num19) / 4f;
                            float num25 = (num22 - num20) / 4f;
                            float num26 = num19;
                            float num27 = num20;
                            for (int num28 = 0; num28 < 4; num28++)
                            {
                                float num29 = (num27 - num26) / 8f;
                                float num30 = num26;
                                int num31 = num23 + n * 4;
                                int num32 = num28 + num13 * 4;
                                int x3 = x1 + num31;
                                int z3 = z1 + num32;
                                float x4 = grid2d.Get(num31, num32);
                                float num33 = grid2d2.Get(num31, num32);
                                int temperatureFast = chunk.GetTemperatureFast(x3, z3);
                                int humidityFast = chunk.GetHumidityFast(x3, z3);
                                float f = num33 - 0.01f * (float)humidityFast;
                                float num34 = MathUtils.Lerp(100f, 0f, f);
                                float num35 = MathUtils.Lerp(300f, 30f, f);
                                bool flag = (temperatureFast > 8 && humidityFast < 8 && num33 < 0.97f) || (MathUtils.Abs(x4) < 16f && num33 < 0.97f);
                                int num36 = TerrainChunk.CalculateCellIndex(x3, 0, z3);
                                for (int num37 = 0; num37 < 8; num37++)
                                {
                                    int num38 = num37 + num14 * 8;
                                    int value = 0;
                                    if (num30 < 0f)
                                    {
                                        if (num38 <= oceanLevel)
                                        {
                                            value = 92;
                                        }
                                    }
                                    else
                                    {
                                        value = ((!flag) ? ((!(num30 < num35)) ? 67 : 3) : ((!(num30 < num34)) ? ((!(num30 < num35)) ? 67 : 3) : 4));
                                    }
                                    chunk.SetCellValueFast(num36 + num38, value);
                                    num30 += num29;
                                }
                                num26 += num24;
                                num27 += num25;
                            }
                            num19 += num15;
                            num20 += num16;
                            num21 += num17;
                            num22 += num18;
                        }
                    }
                }
            }
        }
    }

    //沙漠世界构造
    public class DesertTerrainGenerator : TerrainContentsGenerator22, ITerrainContentsGenerator
    {
        public SubsystemTerrain subsystemTerrain;

        public DesertTerrainGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
        {
            this.subsystemTerrain = subsystemTerrain;
        }

        public new void GenerateChunkContentsPass1(TerrainChunk chunk)
        {
            NGenerateSurfaceParameters(chunk, 0, 0, 16, 8);
            NGenerateTerrain(chunk, 0, 0, 16, 8);
        }

        public new void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
            NGenerateSurfaceParameters(chunk, 0, 8, 16, 16);
            NGenerateTerrain(chunk, 0, 8, 16, 16);
        }

        public new void GenerateChunkContentsPass3(TerrainChunk chunk)
        {
            GenerateCaves(chunk);
            GeneratePockets(chunk);
            GenerateMinerals(chunk);
            PropagateFluidsDownwards(chunk);
        }

        public new void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            int cx = chunk.Coords.X;
            int cy = chunk.Coords.Y;
            if (!((cy - 4 * cx) % 5 == 0 && (cy + 7 * cx) % 4 == 0 && ((int)MathUtils.Sqrt(cx * cx + cy * cy)) % 7 == 0))
            {
                GenerateCacti(chunk);
                return;
            }
            int level = 10;
            bool canGenerate = true;
            for (int y = 200; y > 10; y--)
            {
                int id = subsystemTerrain.Terrain.GetCellContentsFast(chunk.Origin.X, y, chunk.Origin.Y);
                if (id != 0)
                {
                    level = y;
                    break;
                }
            }
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int id1 = subsystemTerrain.Terrain.GetCellContentsFast(chunk.Origin.X + x, level, chunk.Origin.Y + z);
                    int id2 = subsystemTerrain.Terrain.GetCellContentsFast(chunk.Origin.X + x, level + 1, chunk.Origin.Y + z);
                    if (!(id1 != 0 && id1 != 18 && id2 == 0))
                    {
                        canGenerate = false;
                        break;
                    }
                }
            }
            if (canGenerate)
            {
                string blocks = ContentManager.Get<string>("沙漠房子");
                blocks = blocks.Replace("\n", "#");
                string[] blockArray = blocks.Split(new char[1] { '#' });
                foreach (string blockLine in blockArray)
                {
                    string[] block = blockLine.Split(new char[1] { ',' });
                    if (block.Length > 3)
                    {
                        int x = int.Parse(block[0]);
                        int y = int.Parse(block[1]);
                        int z = int.Parse(block[2]);
                        int i = int.Parse(block[3]);
                        subsystemTerrain.Terrain.SetCellValueFast(chunk.Origin.X + x, level + 1 + y, chunk.Origin.Y + z, i);
                    }
                }
            }
            else
            {
                GenerateCacti(chunk);
            }
        }

        public void NGenerateSurfaceParameters(TerrainChunk chunk, int x1, int z1, int x2, int z2)
        {
            for (int i = x1; i < x2; i++)
            {
                for (int j = z1; j < z2; j++)
                {
                    int x = i + chunk.Origin.X;
                    int z = j + chunk.Origin.Y;
                    int temperature = MathUtils.Clamp((int)(MathUtils.Saturate(3f * SimplexNoise.OctavedNoise(x + m_temperatureOffset.X, z + m_temperatureOffset.Y, 0.0015f / TGBiomeScaling, 5, 2f, 0.6f) - 1.1f + m_worldSettings.TemperatureOffset / 16f) * 16f), 12, 15);
                    int humidity = MathUtils.Clamp((int)(MathUtils.Saturate(3f * SimplexNoise.OctavedNoise(x + m_humidityOffset.X, z + m_humidityOffset.Y, 0.0012f / TGBiomeScaling, 5, 2f, 0.6f) - 0.9f + m_worldSettings.HumidityOffset / 16f) * 16f), 0, 3);
                    chunk.SetTemperatureFast(i, j, temperature);
                    chunk.SetHumidityFast(i, j, humidity);
                }
            }
        }

        public void NGenerateTerrain(TerrainChunk chunk, int x1, int z1, int x2, int z2)
        {
            int num = x2 - x1;
            int num2 = z2 - z1;
            _ = m_subsystemTerrain.Terrain;
            int num3 = chunk.Origin.X + x1;
            int num4 = chunk.Origin.Y + z1;
            Grid2d grid2d = new Grid2d(num, num2);
            Grid2d grid2d2 = new Grid2d(num, num2);
            for (int i = 0; i < num2; i++)
            {
                for (int j = 0; j < num; j++)
                {
                    grid2d.Set(j, i, CalculateOceanShoreDistance(j + num3, i + num4));
                    grid2d2.Set(j, i, CalculateMountainRangeFactor(j + num3, i + num4));
                }
            }
            Grid3d grid3d = new Grid3d(num / 4 + 1, 33, num2 / 4 + 1);
            for (int k = 0; k < grid3d.SizeX; k++)
            {
                for (int l = 0; l < grid3d.SizeZ; l++)
                {
                    int num5 = k * 4 + num3;
                    int num6 = l * 4 + num4;
                    float num7 = CalculateHeight(num5, num6);
                    float v = CalculateMountainRangeFactor(num5, num6);
                    float num8 = MathUtils.Lerp(TGMinTurbulence, 1f, Squish(v, TGTurbulenceZero, 1f));
                    for (int m = 0; m < grid3d.SizeY; m++)
                    {
                        int num9 = m * 8;
                        float num10 = TGTurbulenceStrength * num8 * MathUtils.Saturate(num7 - (float)num9) * (2f * SimplexNoise.OctavedNoise(num5, num9, num6, TGTurbulenceFreq, TGTurbulenceOctaves, 4f, TGTurbulencePersistence) - 1f);
                        float num11 = (float)num9 + num10;
                        float num12 = num7 - num11;
                        num12 += MathUtils.Max(4f * (TGDensityBias - (float)num9), 0f);
                        grid3d.Set(k, m, l, num12);
                    }
                }
            }
            int oceanLevel = OceanLevel;
            for (int n = 0; n < grid3d.SizeX - 1; n++)
            {
                for (int num13 = 0; num13 < grid3d.SizeZ - 1; num13++)
                {
                    for (int num14 = 0; num14 < grid3d.SizeY - 1; num14++)
                    {
                        grid3d.Get8(n, num14, num13, out float v2, out float v3, out float v4, out float v5, out float v6, out float v7, out float v8, out float v9);
                        float num15 = (v3 - v2) / 4f;
                        float num16 = (v5 - v4) / 4f;
                        float num17 = (v7 - v6) / 4f;
                        float num18 = (v9 - v8) / 4f;
                        float num19 = v2;
                        float num20 = v4;
                        float num21 = v6;
                        float num22 = v8;
                        for (int num23 = 0; num23 < 4; num23++)
                        {
                            float num24 = (num21 - num19) / 4f;
                            float num25 = (num22 - num20) / 4f;
                            float num26 = num19;
                            float num27 = num20;
                            for (int num28 = 0; num28 < 4; num28++)
                            {
                                float num29 = (num27 - num26) / 8f;
                                float num30 = num26;
                                int num31 = num23 + n * 4;
                                int num32 = num28 + num13 * 4;
                                int x3 = x1 + num31;
                                int z3 = z1 + num32;
                                float x4 = grid2d.Get(num31, num32);
                                float num33 = grid2d2.Get(num31, num32);
                                int temperatureFast = chunk.GetTemperatureFast(x3, z3);
                                int humidityFast = chunk.GetHumidityFast(x3, z3);
                                float f = num33 - 0.01f * (float)humidityFast;
                                float num34 = MathUtils.Lerp(100f, 0f, f);
                                float num35 = MathUtils.Lerp(300f, 30f, f);
                                bool flag = (temperatureFast > 8 && humidityFast < 8 && num33 < 0.97f) || (MathUtils.Abs(x4) < 16f && num33 < 0.97f);
                                int num36 = TerrainChunk.CalculateCellIndex(x3, 0, z3);
                                for (int num37 = 0; num37 < 8; num37++)
                                {
                                    int num38 = num37 + num14 * 8;
                                    int value = 0;
                                    if (num30 >= 0f)
                                    {
                                        value = ((!flag) ? ((!(num30 < num35)) ? 67 : 3) : ((!(num30 < num34)) ? ((!(num30 < num35)) ? 67 : 3) : 4));
                                    }
                                    chunk.SetCellValueFast(num36 + num38, value);
                                    num30 += num29;
                                }
                                num26 += num24;
                                num27 += num25;
                            }
                            num19 += num15;
                            num20 += num16;
                            num21 += num17;
                            num22 += num18;
                        }
                    }
                }
            }
        }
    }

    //冰雪世界构造
    public class SnowfieldTerrainGenerator : TerrainContentsGenerator22, ITerrainContentsGenerator
    {
        public SubsystemTerrain subsystemTerrain;

        public SnowfieldTerrainGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
        {
            this.subsystemTerrain = subsystemTerrain;
        }

        public new void GenerateChunkContentsPass1(TerrainChunk chunk)
        {
            NGenerateSurfaceParameters(chunk, 0, 0, 16, 8);
            GenerateTerrain(chunk, 0, 0, 16, 8);
        }

        public new void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
            NGenerateSurfaceParameters(chunk, 0, 8, 16, 16);
            GenerateTerrain(chunk, 0, 8, 16, 16);
        }

        public new void GenerateChunkContentsPass3(TerrainChunk chunk)
        {
            GenerateCaves(chunk);
            GeneratePockets(chunk);
            GenerateMinerals(chunk);
            GenerateSurface(chunk);
            PropagateFluidsDownwards(chunk);
        }

        public new void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            GenerateGrassAndPlants(chunk);
            GenerateTreesAndLogs(chunk);
            GenerateCacti(chunk);
            GeneratePumpkins(chunk);
            GenerateKelp(chunk);
            GenerateSeagrass(chunk);
            GenerateBottomSuckers(chunk);
            GenerateTraps(chunk);
            GenerateIvy(chunk);
            GenerateGraves(chunk);
            GenerateSnowAndIce(chunk);
            GenerateBedrockAndAir(chunk);
            UpdateFluidIsTop(chunk);
        }

        public void NGenerateSurfaceParameters(TerrainChunk chunk, int x1, int z1, int x2, int z2)
        {
            for (int i = x1; i < x2; i++)
            {
                for (int j = z1; j < z2; j++)
                {
                    int x = i + chunk.Origin.X;
                    int z = j + chunk.Origin.Y;
                    int temperature = 0;
                    int humidity = 15;
                    chunk.SetTemperatureFast(i, j, temperature);
                    chunk.SetHumidityFast(i, j, humidity);
                }
            }
        }
    }

    //限制世界构造
    public class LimitTerrainGenerator : TerrainContentsGenerator22, ITerrainContentsGenerator
    {
        public SubsystemTerrain subsystemTerrain;

        public Vector3 playerSpawnPosition;

        public LimitTerrainGenerator(SubsystemTerrain subsystemTerrain) : base(subsystemTerrain)
        {
            this.subsystemTerrain = subsystemTerrain;
            playerSpawnPosition = FindCoarseSpawnPosition();
        }

        public new void GenerateChunkContentsPass1(TerrainChunk chunk)
        {
            NGenerateSurfaceParameters(chunk);
            NGenerateTerrain(chunk);
        }

        public new void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
        }

        public new void GenerateChunkContentsPass3(TerrainChunk chunk)
        {
            GenerateCaves(chunk);
            //GeneratePockets(chunk);
            GenerateMinerals(chunk);
            GenerateSurface(chunk);
            PropagateFluidsDownwards(chunk);
        }

        public new void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            GenerateGrassAndPlants(chunk);
            GenerateTreesAndLogs(chunk);
            GenerateCacti(chunk);
            GeneratePumpkins(chunk);
            GenerateKelp(chunk);
            GenerateSeagrass(chunk);
            GenerateBottomSuckers(chunk);
            GenerateTraps(chunk);
            GenerateIvy(chunk);
            GenerateGraves(chunk);
            //GenerateSnowAndIce(chunk);
            //GenerateBedrockAndAir(chunk);
            UpdateFluidIsTop(chunk);
        }

        public void NGenerateSurfaceParameters(TerrainChunk chunk)
        {
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    int num = i + chunk.Origin.X;
                    int num2 = j + chunk.Origin.Y;
                    int temperature = CalculateTemperature(num, num2);
                    int humidity = CalculateHumidity(num, num2);
                    chunk.SetTemperatureFast(i, j, temperature);
                    chunk.SetHumidityFast(i, j, humidity);
                }
            }
        }

        public void NGenerateTerrain(TerrainChunk chunk)
        {
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    int x = i + chunk.Origin.X;
                    int z = j + chunk.Origin.Y;
                    float sr = (int)(x - playerSpawnPosition.X) * (x - playerSpawnPosition.X) + (z - playerSpawnPosition.Z) * (z - playerSpawnPosition.Z);
                    //生成倒圆锥
                    for (int k = 0; k < 100; k++)
                    {
                        int y = k - 34;
                        if (sr < k * k && y > 0)
                        {
                            chunk.SetCellValueFast(i, y, j, 2);
                        }
                    }
                    //生成空气墙
                    for (int l = 0; l < 255; l++)
                    {
                        if (sr >= 10000 && sr <= 105 * 105)
                        {
                            chunk.SetCellValueFast(i, l, j, 1023);
                        }
                    }
                }
            }
        }
    }
}