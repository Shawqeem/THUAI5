﻿using System;
using System.Threading;
using System.Collections.Generic;
using GameClass.GameObj;
using Preparation.GameData;
using Preparation.Utility;
using GameEngine;

namespace Gaming
{
    public partial class Game
    {
        private AttackManager attackManager;
        private class AttackManager
        {
            Map gameMap;
            MoveEngine moveEngine;
            public AttackManager(Map gameMap)
            {
                this.gameMap = gameMap;
                this.moveEngine = new MoveEngine
                (
                    gameMap: gameMap,
                    OnCollision: (obj, collisionObj, moveVec) =>
                     {
                         BulletBomb((Bullet)obj, (GameObj)collisionObj);
                         return MoveEngine.AfterCollision.Destroyed;
                     },
                    EndMove: obj =>
                     {
                         Debugger.Output(obj, " end move at " + obj.Position.ToString() + " At time: " + Environment.TickCount64);
                         BulletBomb((Bullet)obj, null);
                     }
                );
            }
            private void BombOnePlayer(Bullet bullet, Character playerBeingShot)
            {
                if (playerBeingShot.BeAttack(bullet))
                {
                    playerBeingShot.CanMove = false;
                    playerBeingShot.IsResetting = true;
                    gameMap.PlayerListLock.EnterWriteLock();
                    try
                    {
                        gameMap.PlayerList.Remove(playerBeingShot);
                    }
                    finally
                    {
                        gameMap.PlayerListLock.ExitWriteLock();
                    }
                    playerBeingShot.Reset();
                    ((Character?)bullet.Parent)?.AddScore(GameData.addScoreWhenKillOneLevelPlayer);  //给击杀者加分

                    new Thread
                        (() =>
                        {

                            Thread.Sleep(GameData.reviveTime);

                            playerBeingShot.AddShield(GameData.shieldTimeAtBirth);  //复活加个盾

                            gameMap.PlayerListLock.EnterWriteLock();
                            try
                            {
                                gameMap.PlayerList.Add(playerBeingShot);
                            }
                            finally { gameMap.PlayerListLock.ExitWriteLock(); }

                            if (gameMap.Timer.IsGaming)
                            {
                                playerBeingShot.CanMove = true;
                            }
                            playerBeingShot.IsResetting = false;
                        }
                        )
                    { IsBackground = true }.Start();
                }
            }
            private void BulletBomb(Bullet bullet, GameObj? objBeingShot)
            {
                Debugger.Output(bullet, "bombed!");
                bullet.CanMove = false;
                gameMap.BulletListLock.EnterWriteLock();
                try
                {
                    foreach (ObjOfCharacter _bullet in gameMap.BulletList)
                    {
                        if (_bullet.ID == bullet.ID)
                        {
                            gameMap.BulletList.Remove(_bullet);
                            break;
                        }
                    }
                }
                finally { gameMap.BulletListLock.ExitWriteLock(); }
                if (objBeingShot != null)
                {
                    if (objBeingShot is Character)
                    {
                        BombOnePlayer(bullet, (Character)objBeingShot);
                        bullet.Parent.HP = (int)(bullet.Parent.HP + bullet.Parent.Vampire * bullet.AP);  //造成伤害根据吸血率来吸血
                    }
                    /*else if (objBeingShot is Bullet)        //子弹不能相互引爆，若要更改这一设定，取消注释即可。
                    {
                        new Thread(() => { BulletBomb((Bullet)objBeingShot, null); }) { IsBackground = true }.Start();
                    }*/

                }

                //子弹爆炸会发生的事↓↓↓
                var beAttackedList = new List<Character>();
                gameMap.PlayerListLock.EnterWriteLock();
                try
                {
                    foreach (Character player in gameMap.PlayerList)
                    {
                        if (bullet.CanAttack(player))
                        {
                            beAttackedList.Add(player);
                        }
                    }
                }
                finally { gameMap.PlayerListLock.ExitReadLock(); }
                foreach (Character beAttackedPlayer in beAttackedList)
                {
                    BombOnePlayer(bullet, beAttackedPlayer);
                }
                beAttackedList.Clear();
            }
            public bool Attack(Character player, double angle)  //射出去的子弹泼出去的水（狗头）
            {                                                   //子弹如果没有和其他物体碰撞，将会一直向前直到超出人物的attackRange
                if (player.IsResetting)
                    return false;
                Bullet bullet = player.RemoteAttack
                    (
                        new XYPosition  //子弹紧贴人物生成。
                        (
                            (int)((player.Radius + player.BulletOfPlayer.Radius) * Math.Cos(angle)),
                            (int)((player.Radius + player.BulletOfPlayer.Radius) * Math.Sin(angle))
                        )
                    );
                if (bullet != null)
                {
                    bullet.CanMove = true;
                    gameMap.BulletListLock.EnterReadLock();
                    try
                    {
                        gameMap.BulletList.Add(bullet);
                    }
                    finally { gameMap.BulletListLock.ExitReadLock(); }
                    moveEngine.MoveObj(bullet, (int)((player.AttackRange - player.Radius - player.BulletOfPlayer.Radius) / bullet.MoveSpeed), angle);  //这里时间参数除出来的单位要是ms
                    return true;
                }
                else return false;
            }
        }
    }
}
