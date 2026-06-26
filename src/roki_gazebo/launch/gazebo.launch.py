import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import ExecuteProcess
from launch_ros.actions import Node

def generate_launch_description():
    pkg_roki_gazebo = get_package_share_directory('roki_gazebo')
    urdf_file = os.path.join(pkg_roki_gazebo, 'urdf', 'roki.urdf')
    world_file = os.path.join(pkg_roki_gazebo, 'worlds', 'workbench.world')

    # Read URDF
    with open(urdf_file, 'r') as infp:
        robot_desc = infp.read()

    # Launch Gazebo
    gazebo = ExecuteProcess(
        cmd=['gazebo', '--verbose', world_file, '-s', 'libgazebo_ros_factory.so', '-s', 'libgazebo_ros_init.so'],
        output='screen'
    )

    # Spawn Robot
    spawn_entity = Node(
        package='gazebo_ros',
        executable='spawn_entity.py',
        arguments=['-entity', 'roki', '-file', urdf_file, '-x', '0', '-y', '0', '-z', '0.825'],
        output='screen'
    )

    # Robot State Publisher
    robot_state_publisher = Node(
        package='robot_state_publisher',
        executable='robot_state_publisher',
        name='robot_state_publisher',
        output='both',
        parameters=[{'robot_description': robot_desc}]
    )

    # Camera Server
    camera_server = Node(
        package='roki_gazebo',
        executable='gazebo_camera_server.py',
        name='camera_server',
        output='screen'
    )

    return LaunchDescription([
        gazebo,
        spawn_entity,
        robot_state_publisher,
        camera_server
    ])
