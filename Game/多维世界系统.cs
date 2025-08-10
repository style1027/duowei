using Engine;
using Engine.Graphics;
using GameEntitySystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TemplatesDatabase;
using XmlUtilities;

namespace Game
{
    /*
     * 这个文件的代码无特殊需要不用改动，仅供参考
     * 如果有能力可以改规则，比如传送门开启方式，鼓励创新
     * 多维世界功能的价值最大化是融入完整的生存Mod体系，所以允许Mod开发者套用和更改
     */

    public class SubsystemWorld : Subsystem, IUpdateable
    {
        public SubsystemTerrain m_subsystemTerrain;

        public SubsystemBodies m_subsystemBodies;

        public ComponentPlayer m_componentPlayer;

        public WorldType m_worldType;

        public Dictionary<Point3, WorldDoor> m_worldDoors = new Dictionary<Point3, WorldDoor>();

        private List<Entity> m_creatureEntitys = new List<Entity>();

        private string m_worldPath;

        private bool m_canGenerateDoor;

        private bool m_initialize;

        private float m_lastDtime = 0f;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        //更新方法，每帧执行一次
        public void Update(float dt)
        {
            if (m_componentPlayer == null) return;
            //新世界构建传送门
            if (m_canGenerateDoor)
            {
                m_lastDtime += dt;
                if (m_lastDtime > 1f)
                {
                    GenerateDoor(new Point3(m_componentPlayer.ComponentBody.Position) + new Point3(0, 1, 2));
                    m_canGenerateDoor = false;
                }
            }
            //生成被传送的动物
            if (!m_initialize)
            {
                m_initialize = true;
                Point3 p = new Point3(m_componentPlayer.ComponentBody.Position);
                foreach (Entity creatureEntity in m_creatureEntitys)
                {
                    Point3 v = new Point3(0, 0, 0);
                    int num = 0;
                    while (num <= 5)
                    {
                        int x = p.X + (new Random()).Int(-5, 5);
                        int y = p.Y + num;
                        int z = p.Z + (new Random()).Int(-5, 5);
                        int i = m_subsystemTerrain.Terrain.GetCellContents(x, y, z);
                        if (i == 0)
                        {
                            v = new Point3(x, y, z);
                            break;
                        }
                        else
                        {
                            num++;
                        }
                    }
                    creatureEntity.FindComponent<ComponentFrame>(true).Position = new Vector3(v.X, v.Y, v.Z);
                    creatureEntity.FindComponent<ComponentFrame>(true).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (new Random()).Float(0f, (float)Math.PI * 2f));
                    creatureEntity.FindComponent<ComponentSpawn>(true).SpawnDuration = 0f;
                    base.Project.AddEntity(creatureEntity);
                }
                m_creatureEntitys.Clear();
            }
            //捕捉实体位置（是否进入传送门）
            foreach (WorldDoor worldDoor in m_worldDoors.Values)
            {
                Vector3 v1 = worldDoor.MinPoint;
                Vector3 v2 = worldDoor.MaxPoint;
                //玩家拟进入传送门
                Vector3 p = m_componentPlayer.ComponentBody.Position;
                if (p.X >= v1.X && p.Y >= v1.Y && p.Z >= v1.Z && p.X <= v2.X && p.Y <= v2.Y && p.Z <= v2.Z)
                {
                    if (m_worldType == worldDoor.WorldType)
                    {
                        ChildToMajorWorld();
                    }
                    else
                    {
                        string worldName = GetWorldName(worldDoor.WorldType);
                        if (m_worldType == WorldType.Default)
                        {
                            MajorToChildWorld(worldName);
                        }
                        else
                        {
                            ChildToChildWorld(worldName);
                        }
                    }
                }
                //动物拟进入传送门
                DynamicArray<ComponentBody> dynamicArray = new DynamicArray<ComponentBody>();
                m_subsystemBodies.FindBodiesInArea(v1.XZ - new Vector2(8f), v2.XY + new Vector2(8f), dynamicArray);
                foreach (ComponentBody creatureBody in dynamicArray)
                {
                    Vector3 p2 = creatureBody.Position;
                    if (p2.X >= v1.X && p2.Y >= v1.Y && p2.Z >= v1.Z && p2.X <= v2.X && p2.Y <= v2.Y && p2.Z <= v2.Z)
                    {
                        ComponentPlayer player = creatureBody.Entity.FindComponent<ComponentPlayer>();
                        if (player == null)
                        {
                            string TransmittedAnimalName = creatureBody.Entity.ValuesDictionary.DatabaseObject.Name;
                            base.Project.RemoveEntity(creatureBody.Entity, true);
                            if (m_worldType == worldDoor.WorldType)
                            {
                                ChildToMajorWorld(IsAnimal: true, TransmittedAnimalName);
                            }
                            else
                            {
                                string worldName = GetWorldName(worldDoor.WorldType);
                                if (m_worldType == WorldType.Default)
                                {
                                    MajorToChildWorld(worldName, IsAnimal: true, TransmittedAnimalName);
                                }
                                else
                                {
                                    ChildToChildWorld(worldName, IsAnimal: true, TransmittedAnimalName);
                                }
                            }
                        }
                    }
                }
            }
        }


        //装载方法，进入存档时执行一次
        public override void Load(ValuesDictionary valuesDictionary)
        {
            m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
            GameLoadingScreen gameLoadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading");
            m_worldPath = gameLoadingScreen.m_worldInfo.DirectoryName;
            m_worldType = GetWorldType(m_worldPath);
            valuesDictionary.SetValue<string>("WorldPath", m_worldPath);
            m_canGenerateDoor = false;
            m_initialize = false;
            //获取从其他世界传送来的动物集合
            if (valuesDictionary.ContainsKey("Creatures"))
            {
                string Creatures = valuesDictionary.GetValue<string>("Creatures");
                if (Creatures != "Null")
                {
                    string[] CreatureArray = Creatures.Split(new char[1] { ',' });
                    foreach (string creatureName in CreatureArray)
                    {
                        Entity entity = DatabaseManager.CreateEntity(base.Project, creatureName, true);
                        m_creatureEntitys.Add(entity);
                    }
                    valuesDictionary.SetValue<string>("Creatures", "Null");
                }
            }
            base.Load(valuesDictionary);
        }

        //保存方法，退出存档时执行一次，且理论每120s执行一次
        public override void Save(ValuesDictionary valuesDictionary)
        {
            //保存当前世界的存档路径
            valuesDictionary.SetValue<string>("WorldPath", m_worldPath);
            base.Save(valuesDictionary);
        }

        //实体添加方法，每当实体被添加时执行一次
        public override void OnEntityAdded(Entity entity)
        {
            ComponentPlayer componentPlayer = entity.FindComponent<ComponentPlayer>();
            if (componentPlayer != null)
            {
                m_componentPlayer = componentPlayer;
                if (m_componentPlayer.PlayerData.SpawnPosition == new Vector3(0, 0, 0))
                {
                    Vector3 coarsePosition = m_subsystemTerrain.TerrainContentsGenerator.FindCoarseSpawnPosition();
                    m_componentPlayer.PlayerData.SpawnPosition = coarsePosition;
                    m_componentPlayer.ComponentBody.Position = coarsePosition;
                    m_canGenerateDoor = true;
                }
            }
            base.OnEntityAdded(entity);
        }

        //实体移除方法，每当实体被移除时执行一次
        public override void OnEntityRemoved(Entity entity)
        {
            base.OnEntityRemoved(entity);
        }

        //释放方法，关闭存档时执行一次
        public override void Dispose()
        {
            base.Dispose();
        }

        //子世界传送到主世界
        public void ChildToMajorWorld(bool IsAnimal = false, string AnimalName = null)
        {
            string path = GameManager.WorldInfo.DirectoryName;
            string wpath = Storage.GetDirectoryName(path);
            if (!IsChildWorld(path))
            {
                m_componentPlayer?.ComponentGui.DisplaySmallMessage("提示：当前世界不为子世界", Color.Yellow, false, false);
                return;
            }
            if (!IsAnimal)
            {
                ChangeWorld(path, wpath);
            }
            else
            {
                SaveCreatures(wpath, AnimalName);
            }
        }

        //主世界传送到子世界
        public void MajorToChildWorld(string worldName, bool IsAnimal = false, string AnimalName = null)
        {
            string path = GameManager.WorldInfo.DirectoryName;
            string wpath = Storage.CombinePaths(path, worldName);
            if (IsChildWorld(path))
            {
                m_componentPlayer?.ComponentGui.DisplaySmallMessage("提示：当前世界不为主世界", Color.Yellow, false, false);
                return;
            }
            if (!IsChildWorld(wpath))
            {
                m_componentPlayer?.ComponentGui.DisplaySmallMessage("提示：传送世界不为子世界", Color.Yellow, false, false);
                return;
            }
            if (!IsAnimal)
            {
                ChangeWorld(path, wpath);
            }
            else
            {
                SaveCreatures(wpath, AnimalName);
            }
        }

        //子世界传送到子世界
        public void ChildToChildWorld(string worldName, bool IsAnimal = false, string AnimalName = null)
        {
            string path = GameManager.WorldInfo.DirectoryName;
            string wpath = Storage.CombinePaths(Storage.GetDirectoryName(path), worldName);
            if (!IsChildWorld(path))
            {
                m_componentPlayer?.ComponentGui.DisplaySmallMessage("提示：当前世界不为子世界", Color.Yellow, false, false);
                return;
            }
            if (!IsChildWorld(wpath))
            {
                m_componentPlayer?.ComponentGui.DisplaySmallMessage("提示：传送世界不为子世界", Color.Yellow, false, false);
                return;
            }
            if (!IsAnimal)
            {
                ChangeWorld(path, wpath);
            }
            else
            {
                SaveCreatures(wpath, AnimalName);
            }
        }

        //切换世界
        public void ChangeWorld(string path, string wpath, bool IsAnimal = false)
        {
            bool isNewSubWorld = false;
            bool isExtendWorld = false;
            if (!Storage.DirectoryExists(wpath))
            {
                Storage.CreateDirectory(wpath);
                //读取外部存档
                Dictionary<string, Stream> fileEntries = GetScworldList(ModsManager.ModsPath);
                string worldName = Storage.GetFileNameWithoutExtension(wpath);
                if (fileEntries.ContainsKey(worldName))
                {
                    WorldsManager.UnpackWorld(wpath, fileEntries[worldName], importEmbeddedExternalContent: true);
                    isExtendWorld = true;
                }
                if (!isExtendWorld)
                {
                    //创建新的子世界存档
                    isNewSubWorld = true;
                    WorldSettings worldSettings = GameManager.WorldInfo.WorldSettings;
                    int num;
                    if (string.IsNullOrEmpty(worldSettings.Seed))
                    {
                        num = (int)(long)(Time.RealTime * 1000.0);
                    }
                    else if (worldSettings.Seed == "0")
                    {
                        num = 0;
                    }
                    else
                    {
                        num = 0;
                        int num2 = 1;
                        string seed = worldSettings.Seed;
                        foreach (char c in seed)
                        {
                            num += c * num2;
                            num2 += 29;
                        }
                    }
                    ValuesDictionary valuesDictionary = new ValuesDictionary();
                    worldSettings.Save(valuesDictionary, liveModifiableParametersOnly: false);
                    valuesDictionary.SetValue("WorldDirectoryName", wpath);
                    valuesDictionary.SetValue("WorldSeed", num);
                    ValuesDictionary valuesDictionary2 = new ValuesDictionary();
                    valuesDictionary2.SetValue("Players", new ValuesDictionary());
                    DatabaseObject databaseObject = DatabaseManager.GameDatabase.Database.FindDatabaseObject("GameProject", DatabaseManager.GameDatabase.ProjectTemplateType, throwIfNotFound: true);
                    XElement xElement = new XElement("Project");
                    XmlUtils.SetAttributeValue(xElement, "Guid", databaseObject.Guid);
                    XmlUtils.SetAttributeValue(xElement, "Name", "GameProject");
                    XmlUtils.SetAttributeValue(xElement, "Version", VersionsManager.SerializationVersion);
                    XElement xElement2 = new XElement("Subsystems");
                    xElement.Add(xElement2);
                    XElement xElement3 = new XElement("Values");
                    XmlUtils.SetAttributeValue(xElement3, "Name", "GameInfo");
                    valuesDictionary.Save(xElement3);
                    xElement2.Add(xElement3);
                    XElement xElement4 = new XElement("Values");
                    XmlUtils.SetAttributeValue(xElement4, "Name", "Players");
                    valuesDictionary2.Save(xElement4);
                    xElement2.Add(xElement4);
                    XElement xElement5 = new XElement("Values");
                    XmlUtils.SetAttributeValue(xElement5, "Name", "PlayerStats");
                    valuesDictionary2.Save(xElement5);
                    xElement2.Add(xElement5);
                    using (Stream stream = Storage.OpenFile(Storage.CombinePaths(wpath, "Project.xml"), OpenFileMode.Create))
                    {
                        XmlUtils.SaveXmlToStream(xElement, stream, null, throwOnError: true);
                    }
                }
            }
            //获取子世界的Info对象，关闭当前存档
            WorldInfo subworldInfo = WorldsManager.GetWorldInfo(wpath);
            GameManager.SaveProject(true, true);
            GameManager.DisposeProject();
            try
            {
                //获取当前存档与目标存档的XElement对象
                XElement rxElement = null;
                XElement subXElement = null;
                using (Stream stream = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Read))
                {
                    rxElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
                }
                using (Stream stream = Storage.OpenFile(Storage.CombinePaths(wpath, "Project.xml"), OpenFileMode.Read))
                {
                    subXElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
                }
                //同步玩家基本数据
                XElement statsElement = null;
                foreach (XElement element in rxElement.Element("Subsystems").Elements())
                {
                    if (XmlUtils.GetAttributeValue<string>(element, "Name") == "PlayerStats")
                    {
                        statsElement = element;
                        break;
                    }
                }
                foreach (XElement element in subXElement.Element("Subsystems").Elements())
                {
                    if (XmlUtils.GetAttributeValue<string>(element, "Name") == "PlayerStats")
                    {
                        ReplaceNodes(statsElement, element, null);
                        break;
                    }
                }
                if (isNewSubWorld)
                {
                    //同步玩家信息
                    XElement playersElement = rxElement.Element("Subsystems").Elements().ElementAt(1);
                    ReplaceNodes(playersElement, subXElement.Element("Subsystems").Elements().ElementAt(1), null);
                    XElement subPlayerElement = subXElement.Element("Subsystems").Elements().ElementAt(1).Elements().ElementAt(2);
                    foreach (XElement element in subPlayerElement.Element("Values").Elements())
                    {
                        string elementName = XmlUtils.GetAttributeValue<string>(element, "Name");
                        if (elementName == "SpawnPosition")
                        {
                            XmlUtils.SetAttributeValue(element, "Value", new Point3(0, 0, 0));
                            break;
                        }
                    }
                    //同步玩家实体
                    XElement playerEntityElement = rxElement.Element("Entities").Element("Entity");
                    subXElement.Add(new XElement("Entities"));
                    subXElement.Element("Entities").Add(playerEntityElement);
                }
                else
                {
                    //同步玩家信息
                    XElement playerElement = null;
                    foreach (XElement element in rxElement.Element("Subsystems").Elements().ElementAt(1).Elements())
                    {
                        if (XmlUtils.GetAttributeValue<string>(element, "Name") == "Players")
                        {
                            playerElement = element;
                            break;
                        }
                    }
                    foreach (XElement element in subXElement.Element("Subsystems").Elements().ElementAt(1).Elements())
                    {
                        if (XmlUtils.GetAttributeValue<string>(element, "Name") == "Players")
                        {
                            string[] parameters = { "SpawnPosition" };
                            ReplaceNodes(playerElement.Element("Values"), element.Element("Values"), parameters);
                            break;
                        }
                    }
                    //同步玩家实体
                    XElement playerEntityElement = rxElement.Element("Entities").Element("Entity");
                    string[] rparameters = { "Body" };
                    ReplaceNodes(playerEntityElement, subXElement.Element("Entities").Element("Entity"), rparameters);
                }
                //保存同步后的目标存档
                using (Stream stream2 = Storage.OpenFile(Storage.CombinePaths(wpath, "Project.xml"), OpenFileMode.Create))
                {
                    XmlUtils.SaveXmlToStream(subXElement, stream2, null, throwOnError: true);
                }
                //同步存档基本数据
                SynchronizeGameInfo(rxElement, subXElement, path, wpath, isNewSubWorld);
                //保存目标存档路径
                m_worldPath = wpath;
                if (IsChildWorld(path))
                {
                    path = Storage.GetDirectoryName(path);
                }
                SavePathToMajorWorld(path, m_worldPath);
            }
            catch (Exception)
            {
                //System.Diagnostics.Debug.WriteLine(e.Message);
            }
            finally
            {
                //加载目标存档
                ScreensManager.SwitchScreen("GameLoading", subworldInfo, null);
            }
        }

        //创建完整传送门
        public void GenerateDoor(Point3 position)
        {
            int cid = GetWorldDoorBlock(m_worldType);
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    int id = 0;
                    if (x == 0 || x == 3) id = cid;
                    if ((x == 1 || x == 2) && (y == 0 || y == 4)) id = cid;
                    m_subsystemTerrain.ChangeCell(position.X + x, position.Y + y, position.Z, id);
                }
            }
            SubsystemTransferBlockBehavior transferBlockBehavior = base.Project.FindSubsystem<SubsystemTransferBlockBehavior>(true);
            transferBlockBehavior.CreateEntrance(position + new Point3(1, 0, 0), position + new Point3(2, 0, 0), true, m_worldType);
        }

        //判断是否为子世界
        public static bool IsChildWorld(string cpath)
        {
            string path = Storage.GetDirectoryName(cpath);
            return (path != WorldsManager.WorldsDirectoryName && Storage.GetDirectoryName(path) == WorldsManager.WorldsDirectoryName);
        }

        //获取外部存档
        public static Dictionary<string, Stream> GetScworldList(string path)
        {
            Dictionary<string, Stream> fileEntrys = new Dictionary<string, Stream>();
            foreach (string fname in Storage.ListFileNames(path))
            {
                string extension = Storage.GetExtension(fname);
                string pathName = Storage.CombinePaths(path, fname);
                Stream stream = Storage.OpenFile(pathName, OpenFileMode.Read);
                try
                {
                    if (extension == ".scmod")
                    {
                        ZipArchive zipArchive = ZipArchive.Open(stream, true);
                        foreach (ZipArchiveEntry zipArchiveEntry in zipArchive.ReadCentralDir())
                        {
                            if (Storage.GetExtension(zipArchiveEntry.FilenameInZip) == ".scworld")
                            {
                                MemoryStream memoryStream = new MemoryStream();
                                zipArchive.ExtractFile(zipArchiveEntry, memoryStream);
                                memoryStream.Position = 0L;
                                string filename = Storage.GetFileNameWithoutExtension(zipArchiveEntry.FilenameInZip);
                                fileEntrys.Add(filename, memoryStream);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            return fileEntrys;
        }

        //替换XElement对象的子元素
        public static void ReplaceNodes(XElement replaceElement, XElement sourceElement, string[] reserveParameters)
        {
            Dictionary<string, XElement> valuePairs = new Dictionary<string, XElement>();
            if (reserveParameters != null)
            {
                foreach (XElement element in sourceElement.Elements())
                {
                    string attributeName = XmlUtils.GetAttributeValue<string>(element, "Name");
                    foreach (string parameter in reserveParameters)
                    {
                        if (attributeName == parameter)
                        {
                            valuePairs.Add(attributeName, element);
                            break;
                        }
                    }
                }
            }
            sourceElement.RemoveNodes();
            if (reserveParameters != null)
            {
                foreach (XElement element in replaceElement.Elements())
                {
                    string attributeName = XmlUtils.GetAttributeValue<string>(element, "Name");
                    if (valuePairs.ContainsKey(attributeName))
                    {
                        sourceElement.Add(valuePairs[attributeName]);
                    }
                    else
                    {
                        sourceElement.Add(element);
                    }
                }
            }
            else
            {
                foreach (XElement element in replaceElement.Elements())
                {
                    sourceElement.Add(element);
                }
            }
        }

        //同步存档GameInfo
        public static void SynchronizeGameInfo(XElement rxElement, XElement subXElement, string path, string wpath, bool newWorld)
        {
            XElement gameInfoElement = null;
            if (newWorld)
            {
                if (IsChildWorld(path)) path = Storage.GetDirectoryName(path);
                using (Stream stream = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Read))
                {
                    rxElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
                }
            }
            foreach (XElement element in rxElement.Element("Subsystems").Elements())
            {
                if (XmlUtils.GetAttributeValue<string>(element, "Name") == "GameInfo")
                {
                    gameInfoElement = element;
                    break;
                }
            }
            foreach (XElement element in subXElement.Element("Subsystems").Elements())
            {
                if (XmlUtils.GetAttributeValue<string>(element, "Name") == "GameInfo")
                {
                    string[] parameters = {
                        "WorldName","OriginalSerializationVersion",
                        "EnvironmentBehaviorMode", "TimeOfDayMode", "AreWeatherEffectsEnabled",
                        "TerrainGenerationMode", "IslandSize", "TerrainLevel",
                        "ShoreRoughness", "TerrainBlockIndex", "TerrainOceanBlockIndex",
                        "TemperatureOffset", "HumidityOffset", "SeaLevelOffset",
                        "BiomeSize", "StartingPositionMode", "BlockTextureName",
                        "Palette", "WorldSeed", "TotalElapsedGameTime"
                    };
                    if (newWorld) parameters = null;
                    ReplaceNodes(gameInfoElement, element, parameters);
                    break;
                }
            }
            using (Stream stream2 = Storage.OpenFile(Storage.CombinePaths(wpath, "Project.xml"), OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(subXElement, stream2, null, throwOnError: true);
            }
        }

        //保存存档路径到主世界存档中
        public static void SavePathToMajorWorld(string path, string wpath)
        {
            XElement xElement = null;
            using (Stream stream = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Read))
            {
                xElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
            }
            foreach (XElement e in xElement.Element("Subsystems").Elements())
            {
                if (XmlUtils.GetAttributeValue<string>(e, "Name") == "SubWorld")
                {
                    XmlUtils.SetAttributeValue(e.Element("Value"), "Value", wpath);
                }
            }
            using (Stream stream2 = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(xElement, stream2, null, throwOnError: true);
            }
        }

        //保存被传送的动物到对应世界
        public static void SaveCreatures(string path, string creatureName)
        {
            XElement xElement = null;
            if (!Storage.DirectoryExists(path)) return;
            using (Stream stream = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Read))
            {
                xElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
            }
            foreach (XElement element in xElement.Element("Subsystems").Elements())
            {
                if (XmlUtils.GetAttributeValue<string>(element, "Name") == "SubWorld")
                {
                    if (element.Elements().Count() == 1)
                    {
                        XElement creatureXElement = new XElement("Value");
                        element.Add(creatureXElement);
                        XmlUtils.SetAttributeValue(element.Elements().ElementAt(1), "Name", "Creatures");
                        XmlUtils.SetAttributeValue(element.Elements().ElementAt(1), "Type", "string");
                        XmlUtils.SetAttributeValue(element.Elements().ElementAt(1), "Value", "Null");
                    }
                    string creatureNames = XmlUtils.GetAttributeValue<string>(element.Elements().ElementAt(1), "Value");
                    if (creatureNames == "Null")
                    {
                        creatureNames = creatureName;
                    }
                    else
                    {
                        creatureNames = creatureNames + "," + creatureName;
                    }
                    XmlUtils.SetAttributeValue(element.Elements().ElementAt(1), "Value", creatureNames);
                    break;
                }
            }
            using (Stream stream2 = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(xElement, stream2, null, throwOnError: true);
            }
        }

        //获取世界类型
        public static WorldType GetWorldType(string pathOrName)
        {
            WorldType worldType = WorldType.Default;
            pathOrName = pathOrName.Replace("\\", "/");
            string[] worldPaths = pathOrName.Split(new char[1] { '/' });
            string worldName = worldPaths[worldPaths.Length - 1];
            foreach (WorldType type in Enum.GetValues(typeof(WorldType)))
            {
                if (type.ToString() == worldName)
                {
                    worldType = type;
                    break;
                }
            }
            return worldType;
        }

        //获取世界名称
        public static string GetWorldName(WorldType worldType)
        {
            string worldName = worldType.ToString();
            //foreach (string[] parameter in WorldParameter.Collections)
            //{
            //    if (worldType.ToString() == parameter[0])
            //    {
            //        worldName = parameter[1];
            //        break;
            //    }
            //}
            return worldName;
        }

        //获取世界对应传送门的方块ID
        public static int GetWorldDoorBlock(WorldType worldType)
        {
            int id = 0;
            foreach (string[] parameter in WorldParameter.Collections)
            {
                if (worldType.ToString() == parameter[0])
                {
                    id = int.Parse(parameter[2]);
                    break;
                }
            }
            return id;
        }

        //获取世界对应传送门贴图颜色
        public static Color GetWorldDoorColor(WorldType worldType)
        {
            Color color = Color.White;
            foreach (string[] parameter in WorldParameter.Collections)
            {
                if (worldType.ToString() == parameter[0])
                {
                    string[] scolor = parameter[3].Split(new char[1] { ',' });
                    int r = int.Parse(scolor[0]);
                    int g = int.Parse(scolor[1]);
                    int b = int.Parse(scolor[2]);
                    int a = int.Parse(scolor[3]);
                    color = new Color(r, g, b, a);
                    break;
                }
            }
            return color;
        }
    }

    public class WorldDoor
    {
        public Vector3 MinPoint; //传送门位置最小点

        public Vector3 MaxPoint; //传送门位置最大点

        public WorldType WorldType; //传送门对应世界类型
    }

    public class Chartlet
    {
        public Vector3 Position;  //贴图位置

        public Color Color;  //贴图颜色

        public Vector3 Right;

        public Vector3 Up;

        public Vector3 Forward;

        public float Size = 1.5f;

        public float FarSize = 1.5f;

        public float FarDistance = 1f;
    }

    public class SubsystemChartlet : Subsystem, IDrawable
    {
        public SubsystemSky m_subsystemSky;

        public Dictionary<Point3, Chartlet[]> m_chartlets = new Dictionary<Point3, Chartlet[]>();

        private PrimitivesRenderer3D PrimitivesRenderer = new PrimitivesRenderer3D();

        private TexturedBatch3D BatchesByType = new TexturedBatch3D();

        public int[] DrawOrders => new int[1] { 110 };

        public void Draw(Camera camera, int drawOrder)
        {
            foreach (Point3 point3 in m_chartlets.Keys)
            {
                Chartlet[] keys = m_chartlets[point3];
                foreach (Chartlet key in keys)
                {
                    Vector3 vector = key.Position - camera.ViewPosition;
                    float num = Vector3.Dot(vector, camera.ViewDirection);
                    if (num > 0.01f)
                    {
                        float num2 = vector.Length();
                        if (num2 < m_subsystemSky.ViewFogRange.Y)
                        {
                            float num3 = key.Size;
                            if (key.FarDistance > 0f)
                            {
                                num3 += (key.FarSize - key.Size) * MathUtils.Saturate(num2 / key.FarDistance);
                            }
                            Vector3 v = (0f - (0.01f + 0.02f * num)) / num2 * vector;
                            Vector3 p = key.Position + num3 * (-key.Right - key.Up) + v;
                            Vector3 p2 = key.Position + num3 * (key.Right - key.Up) + v;
                            Vector3 p3 = key.Position + num3 * (key.Right + key.Up) + v;
                            Vector3 p4 = key.Position + num3 * (-key.Right + key.Up) + v;
                            BatchesByType.QueueQuad(p, p2, p3, p4, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), key.Color);
                        }
                    }
                }
            }
            PrimitivesRenderer.Flush(camera.ViewProjectionMatrix);
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
            BatchesByType = PrimitivesRenderer.TexturedBatch(ContentManager.Get<Texture2D>("传送门贴图"), false, 0, DepthStencilState.DepthRead, RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);
        }

        public void CreateChartlet(Point3 point3, bool IsAxisX, WorldType worldType)
        {
            Chartlet[] chartlets = new Chartlet[2];
            Chartlet chartlet = new Chartlet();
            Chartlet chartlet2 = new Chartlet();
            Color color = SubsystemWorld.GetWorldDoorColor(worldType);
            Vector3 rposition = new Vector3(point3) + new Vector3(1.0f, 2.5f, 0.5f);
            Vector3 forward = new Vector3(0f, 0f, -1f);
            Vector3 up = new Vector3(0f, -1f, 0f);
            Vector3 right = new Vector3(-1f, 0f, 0f);
            Vector3 forward2 = new Vector3(0f, 0f, -1f);
            Vector3 up2 = new Vector3(0f, -1f, 0f);
            Vector3 right2 = new Vector3(1f, 0f, 0f);
            if (!IsAxisX)
            {
                rposition = new Vector3(point3) + new Vector3(0.5f, 2.5f, 1.0f);
                forward = new Vector3(-1f, 0f, 0f);
                up = new Vector3(0f, -1f, 0f);
                right = new Vector3(0f, 0f, -1f);
                forward2 = new Vector3(-1f, 0f, 0f);
                up2 = new Vector3(0f, -1f, 0f);
                right2 = new Vector3(0f, 0f, 1f);
            }
            chartlet.Position = rposition;
            chartlet.Forward = forward;
            chartlet.Up = up;
            chartlet.Right = right;
            chartlet.Color = color;
            chartlet2.Position = rposition;
            chartlet2.Forward = forward2;
            chartlet2.Up = up2;
            chartlet2.Right = right2;
            chartlet2.Color = color;
            chartlets[0] = chartlet;
            chartlets[1] = chartlet2;
            if (!m_chartlets.ContainsKey(point3))
            {
                m_chartlets.Add(point3, chartlets);
            }
        }
    }

    public class SubsystemTransferBlockBehavior : SubsystemBlockBehavior
    {
        public SubsystemWorld m_subsystemWorld;

        public SubsystemChartlet m_subsystemChartlet;

        public SubsystemTerrain m_subsystemTerrain;

        public SubsystemParticles m_subsystemParticles;

        public override int[] HandledBlocks => new int[1] { 789 };

        public override void Load(ValuesDictionary valuesDictionary)
        {
            m_subsystemWorld = base.Project.FindSubsystem<SubsystemWorld>(true);
            m_subsystemChartlet = base.Project.FindSubsystem<SubsystemChartlet>(true);
            m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
        }

        //使用传送石触发
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            object raycastResult = componentMiner.Raycast(ray, RaycastMode.Interaction);
            if (raycastResult is TerrainRaycastResult)
            {
                CellFace cellFace = ((TerrainRaycastResult)raycastResult).CellFace;
                if (cellFace.Face != 4) return false;
                Point3 point = cellFace.Point;
                int id = m_subsystemTerrain.Terrain.GetCellContents(point.X, point.Y, point.Z);
                foreach (string[] parameters in WorldParameter.Collections)
                {
                    if (id == int.Parse(parameters[2]))
                    {
                        string[] colors = parameters[3].Split(new char[1] { ',' });
                        if (colors.Length > 3)
                        {
                            Color color = new Color(int.Parse(colors[0]), int.Parse(colors[1]), int.Parse(colors[2]), int.Parse(colors[3]));
                            m_subsystemParticles.AddParticleSystem(new FireworksParticleSystem(new Vector3(point) + new Vector3(0.5f, 1f, 0.5f), color, FireworksBlock.Shape.SmallBurst, 0.8f, 0.3f));
                            break;
                        }
                    }
                }
                Point3 point2 = point + new Point3(1, 0, 0);
                bool createSuccess = CreateEntrance(point, point2, false, WorldType.Default);
                if (createSuccess) return true;
                point2 = point + new Point3(-1, 0, 0);
                createSuccess = CreateEntrance(point, point2, false, WorldType.Default);
                if (createSuccess) return true;
                point2 = point + new Point3(0, 0, 1);
                createSuccess = CreateEntrance(point, point2, false, WorldType.Default);
                if (createSuccess) return true;
                point2 = point + new Point3(0, 0, -1);
                createSuccess = CreateEntrance(point, point2, false, WorldType.Default);
                if (createSuccess) return true;
            }
            return false;
        }

        //判断搭建方块是否为传送门
        public bool JudgePass(Point3 point, Point3 point2, int cid)
        {
            for (int i = 0; i < 5; i++)
            {
                int id1 = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValue(point.X, point.Y + i, point.Z));
                int id2 = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValue(point2.X, point2.Y + i, point2.Z));
                if ((i == 0 || i == 4) && (id1 != cid || id2 != cid)) return false;
                if (i > 0 && i < 4 && (id1 != 0 || id2 != 0)) return false;
            }
            Point3 point3 = point2 - point;
            point = point - point3;
            point2 = point2 + point3;
            for (int i = 0; i < 5; i++)
            {
                int id3 = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValue(point.X, point.Y + i, point.Z));
                int id4 = Terrain.ExtractContents(m_subsystemTerrain.Terrain.GetCellValue(point2.X, point2.Y + i, point2.Z));
                if (id3 != cid || id4 != cid) return false;
            }
            return true;
        }

        //创建传送门入口
        public bool CreateEntrance(Point3 point, Point3 point2, bool skipJudge, WorldType fastType)
        {
            foreach (string[] parameter in WorldParameter.Collections)
            {
                int cid = int.Parse(parameter[2]);
                bool pass = JudgePass(point, point2, cid);
                if (pass || skipJudge)
                {
                    WorldDoor worldDoor = new WorldDoor();
                    if (point.X - point2.X > 0 || point.Y - point2.Y > 0 || point.Z - point2.Z > 0)
                    {
                        Point3 temp = point;
                        point = point2;
                        point2 = temp;
                    }
                    bool AxisX = false;
                    Vector3 vector = new Vector3(1f, 0, 0);
                    Vector3 vector2 = new Vector3(0, 3f, 1f);
                    if (point2.X - point.X > 0 && point2.Z - point.Z == 0)
                    {
                        AxisX = true;
                        vector = new Vector3(0, 0, 1f);
                        vector2 = new Vector3(1f, 3f, 0);
                    }
                    worldDoor.WorldType = skipJudge ? fastType : SubsystemWorld.GetWorldType(parameter[0]);
                    worldDoor.MinPoint = new Vector3(point) + new Vector3(0, 1f, 0) + vector * 0.4f;
                    worldDoor.MaxPoint = new Vector3(point2) + new Vector3(0, 1f, 0) + vector * 0.6f + vector2;
                    if (!m_subsystemWorld.m_worldDoors.ContainsKey(point))
                    {
                        m_subsystemWorld.m_worldDoors.Add(point, worldDoor);
                        m_subsystemChartlet.CreateChartlet(point, AxisX, worldDoor.WorldType);
                    }
                    return true;
                }
            }
            return false;
        }
    }

    public class TransferBlock : Block
    {
        public const int Index = 789;

        public BlockMesh m_standaloneBlockMesh = new BlockMesh();

        public Color m_color;

        public TransferBlock()
        {
            DefaultDisplayName = "传送石";
            DefaultDescription = "点击传送方块门的门槛位置可召唤出传送门入口";
            DefaultCategory = "Items";
            CraftingId = "transfer";
            DisplayOrder = 1;
            IsPlaceable = false;
            FirstPersonScale = 0.4f;
            FirstPersonOffset = new Vector3(0.5f, -0.5f, -0.6f);
            InHandScale = 0.3f;
            InHandOffset = new Vector3(0, 0.12f, 0);
        }

        public override void Initialize()
        {
            m_color = new Color(192, 255, 128, 192);
            Model model = ContentManager.Get<Model>("Models/Diamond");
            Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Diamond").ParentBone);
            m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Diamond").MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), makeEmissive: false, flipWindingOrder: false, doubleSided: false, flipNormals: false, Color.White);
            base.Initialize();
        }

        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, m_color, 2f * size, ref matrix, environmentData);
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }
    }

    public class LoadScreenLoader : ModLoader 
    {
        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("BeforeGameLoading", this);
        }

        public override void OnLoadingFinished(List<Action> actions)
        {
            actions.Add(delegate 
            {
                CraftingRecipe craftingRecipe = new CraftingRecipe();
                craftingRecipe.Description = "合成传送石";
                craftingRecipe.Message = "提示：等级达到3级才能合成传送石";
                craftingRecipe.Ingredients = new string[9] { null, "experience", null, "experience", "diamond", "experience", null, "experience", null };
                craftingRecipe.ResultValue = 789;
                craftingRecipe.ResultCount = 1;
                craftingRecipe.RemainsValue = 0;
                craftingRecipe.RemainsCount = 0;
                craftingRecipe.RequiredHeatLevel = 0;
                craftingRecipe.RequiredPlayerLevel = 3;
                CraftingRecipesManager.m_recipes.Add(craftingRecipe);
            });
        }

        public override object BeforeGameLoading(PlayScreen playScreen, object item)
        {
            string path = ((WorldInfo)item).DirectoryName;
            string wpath = string.Empty;
            XElement xElement = null;
            XElement subXElement = null;
            using (Stream stream = Storage.OpenFile(Storage.CombinePaths(path, "Project.xml"), OpenFileMode.Read))
            {
                xElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
            }
            foreach (XElement e in xElement.Element("Subsystems").Elements())
            {
                if (XmlUtils.GetAttributeValue<string>(e, "Name") == "SubWorld")
                {
                    wpath = XmlUtils.GetAttributeValue<string>(e.Element("Value"), "Value");
                }
            }
            if (wpath != string.Empty)
            {
                using (Stream stream = Storage.OpenFile(Storage.CombinePaths(wpath, "Project.xml"), OpenFileMode.Read))
                {
                    subXElement = XmlUtils.LoadXmlFromStream(stream, null, throwOnError: true);
                }
                //存档设置与主世界同步
                SubsystemWorld.SynchronizeGameInfo(xElement, subXElement, path, wpath, false);
            }
            else
            {
                wpath = path;
            }
            //进入上一次退出的存档
            GameLoadingScreen gameLoadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading");
            gameLoadingScreen.m_worldInfo = WorldsManager.GetWorldInfo(wpath);
            return gameLoadingScreen.m_worldInfo;
        }
    }
}
